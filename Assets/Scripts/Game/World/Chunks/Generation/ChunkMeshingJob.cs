#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Graphics;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public class ChunkMeshingJob : AsyncJob
    {
        private static readonly ObjectPool<BlockFaces[]> _masksPool = new ObjectPool<BlockFaces[]>();
        private static readonly ObjectPool<MeshData> _meshDataPool = new ObjectPool<MeshData>();

        private readonly Stopwatch _Stopwatch;
        private readonly INodeCollection<ushort>[] _NeighborNodes;

        private float3 _OriginPoint;
        private MeshData _MeshData;
        private BlockFaces[] _Mask;
        private INodeCollection<ushort> _Blocks;
        private bool _AggressiveFaceMerging;
        private TimeSpan _SetBlockTimeSpan;
        private TimeSpan _MeshingTimeSpan;

        public ChunkMeshingJob()
        {
            _Stopwatch = new Stopwatch();
            _NeighborNodes = new INodeCollection<ushort>[6];
        }

        protected override Task Process()
        {
            GenerateMesh();

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!CancellationToken.IsCancellationRequested)
            {
                DiagnosticsController.Current.RollingMeshingSetBlockTimes.Enqueue(_SetBlockTimeSpan);
                DiagnosticsController.Current.RollingMeshingTimes.Enqueue(_MeshingTimeSpan);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Sets the data required for mesh generation.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation indication.</param>
        /// <param name="originPoint">Origin point of the chunk that's being meshed.</param>
        /// <param name="blocks"><see cref="INodeCollection{T}" /> of blocks contained within the chunk.</param>
        /// <param name="aggressiveFaceMerging">Indicates whether to merge similarly textured faces.</param>
        public void SetData(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocks,
            bool aggressiveFaceMerging)
        {
            CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken, cancellationToken).Token;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveFaceMerging;
        }

        /// <summary>
        ///     Clears all the <see cref="ChunkMeshingJob" />'s internal data.
        /// </summary>
        /// <remarks>
        ///     This should be called only after its mesh data has been applied to a mesh.
        ///     This is because the <see cref="MeshData" /> object is cleared and added to the
        ///     internal object pool for use in other jobs.
        /// </remarks>
        public void ClearData()
        {
            // clear existing data from mesh object
            // 'true' for trimming excess data from the lists.
            _MeshData.Clear(true);
            // add the mesh data to the internal object pool
            _meshDataPool.TryAdd(_MeshData);
            // remove the existing reference
            _MeshData = default;

            // clear neighbor nodes to free RAM
            Array.Clear(_NeighborNodes, 0, _NeighborNodes.Length);

            _OriginPoint = default;
            _Blocks = default;
            _AggressiveFaceMerging = default;
            _SetBlockTimeSpan = default;
            _MeshingTimeSpan = default;
        }

        public void ApplyMeshData(ref Mesh mesh) => _MeshData.ApplyMeshData(ref mesh);

        #region Generation

        /// <summary>
        ///     Generates the mesh data.
        /// </summary>
        /// <remarks>
        ///     The generated data is stored in the <see cref="MeshData" /> object <see cref="_MeshData" />.
        /// </remarks>
        private void GenerateMesh()
        {
            if ((_Blocks == null) || (_Blocks.IsUniform && (_Blocks.Value == BlockController.AirID)))
            {
                return;
            }

            // restart stopwatch to measure pre-mesh op time
            _Stopwatch.Restart();

            // retrieve existing objects from object pool
            _MeshData = _meshDataPool.Retrieve() ?? new MeshData(new List<Vector3>(), new List<Vector3>(), new List<int>(), new List<int>());
            _Mask = _masksPool.Retrieve() ?? new BlockFaces[ChunkController.SIZE_CUBED];

            // clear masks for new meshing
            Array.Clear(_Mask, 0, _Mask.Length);

            // set block data for relevant neighbor indexes
            foreach ((int3 normal, ChunkController chunkController) in WorldController.Current.GetNeighboringChunksWithNormal(_OriginPoint))
            {
                int index = -1;

                if (normal.x != 0)
                {
                    index = normal.x > 0 ? 0 : 3;
                }
                else if (normal.y != 0)
                {
                    index = normal.y > 0 ? 1 : 4;
                }
                else if (normal.z != 0)
                {
                    index = normal.z > 0 ? 2 : 5;
                }

                _NeighborNodes[index] = chunkController.Blocks;
            }

            _Stopwatch.Stop();

            _SetBlockTimeSpan = _Stopwatch.Elapsed;

            _Stopwatch.Restart();

            for (int index = 0; index < _Mask.Length; index++)
            {
                // observe cancellation token
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // set local and global position according to index
                int3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);
                int3 globalPosition = WydMath.ToInt(_OriginPoint + localPosition);

                ushort currentBlockId = _Blocks.GetPoint(localPosition);

                if (currentBlockId == BlockController.AirID)
                {
                    continue;
                }

                TraverseIndex(index, globalPosition, localPosition, currentBlockId,
                    BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent));
            }

            // clear mask, add to object pool, and unset reference
            Array.Clear(_Mask, 0, _Mask.Length);
            _masksPool.TryAdd(_Mask);
            _Mask = null;

            _Stopwatch.Stop();

            _MeshingTimeSpan = _Stopwatch.Elapsed;
        }

        private void TraverseIndex(int index, int3 globalPosition, int3 localPosition, ushort currentBlockId, bool transparentTraversal)
        {
            for (int normalIndex = 0; normalIndex < 6; normalIndex++)
            {
                Direction faceDirection = (Direction)(1 << normalIndex);

                if (_Mask[index].HasFace(faceDirection))
                {
                    continue;
                }

                int iModulo3 = normalIndex % 3;
                int traversalIterations = 0;
                int traversals = 0;

                for (int perpendicularNormalIndex = 0; perpendicularNormalIndex < 2; perpendicularNormalIndex++)
                {
                    int traversalNormalAxisIndex = (iModulo3 + 1) % 3;
                    int3 traversalNormal = new int3(0)
                    {
                        [traversalNormalAxisIndex] = 1
                    };

                    int sliceIndexValue = localPosition[traversalNormalAxisIndex];
                    int maximumTraversals = _AggressiveFaceMerging ? ChunkController.SIZE : sliceIndexValue + 1;
                    int traversalFactor = GenerationConstants.IndexStepByTraversalNormalIndex[traversalNormalAxisIndex];

                    int traversalIndex = index + (traversals * traversalFactor);
                    int3 traversalPosition = localPosition +  (traversalNormal * traversals);

                    for (; (sliceIndexValue + traversals) < maximumTraversals;
                        traversals++, traversalIndex += traversalFactor, traversalPosition += traversalNormal)
                    {
                        if (_Mask[traversalIndex].HasFace(faceDirection)
                            || ((traversals > 0) && (_Blocks.GetPoint(traversalPosition) != currentBlockId)))
                        {
                            break;
                        }

                        int3 faceNormal = GenerationConstants.FaceNormalByIteration[normalIndex];
                        int3 traversalFacingBlockPosition = traversalPosition + faceNormal;
                        int facingPositionAxisValue = traversalFacingBlockPosition[iModulo3];

                        ushort facingBlockId;
                        if ((facingPositionAxisValue > 0) && (facingPositionAxisValue < ChunkController.SIZE_MINUS_ONE))
                        {
                            facingBlockId = _Blocks.GetPoint(traversalFacingBlockPosition);
                        }
                        else
                        {
                            int3 relativeLocalPosition = math.abs(traversalFacingBlockPosition + (faceNormal * -ChunkController.SIZE_MINUS_ONE));

                            facingBlockId = _NeighborNodes[normalIndex]?.GetPoint(relativeLocalPosition) ?? BlockController.NullID;
                        }

                        // if transparent, traverse as long as block is the same
                        // if opaque, traverse as long as faceNormal-adjacent block is transparent
                        if ((transparentTraversal && (currentBlockId != facingBlockId))
                            || !BlockController.Current.CheckBlockHasProperty(facingBlockId, BlockDefinition.Property.Transparent))
                        {
                            break;
                        }

                        _Mask[traversalIndex].SetFace(faceDirection);
                    }

                    // if we haven't traversed at all, continue to next axis
                    if ((traversals == 0) || ((traversalIterations == 0) && (traversals == 1)))
                    {
                        traversalIterations += 1;
                        continue;
                    }

                    // add triangles
                    int verticesCount = _MeshData.VerticesCount;
                    int transparentAsInt = Convert.ToInt32(transparentTraversal);

                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (int triangleIndex = 0; triangleIndex < BlockFaces.Triangles.FaceTriangles.Length; triangleIndex++)
                    {
                        _MeshData.AddTriangle(transparentAsInt, BlockFaces.Triangles.FaceTriangles[triangleIndex] + verticesCount);
                    }

                    // add vertices
                    int3 traversalVertexOffset = math.max(traversals * traversalNormal, 1);
                    float3[] vertices = BlockFaces.Vertices.FaceVerticesByNormalIndex[normalIndex];

                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (int verticesIndex = 0; verticesIndex < vertices.Length; verticesIndex++)
                    {
                        _MeshData.AddVertex(localPosition + (vertices[verticesIndex] * traversalVertexOffset));
                    }

                    if (BlockController.Current.GetUVs(currentBlockId, globalPosition, faceDirection, new float2(1f)
                    {
                        [GenerationConstants.UVIndexAdjustments[iModulo3][traversalNormalAxisIndex]] = traversals
                    }, out BlockUVs blockUVs))
                    {
                        _MeshData.AddUV(blockUVs.TopLeft);
                        _MeshData.AddUV(blockUVs.TopRight);
                        _MeshData.AddUV(blockUVs.BottomLeft);
                        _MeshData.AddUV(blockUVs.BottomRight);
                    }

                    break;
                }
            }
        }

        #endregion
    }
}
