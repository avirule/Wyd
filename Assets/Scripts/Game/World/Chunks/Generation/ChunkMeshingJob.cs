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
        private static readonly ObjectPool<ushort[]> _blocksPool = new ObjectPool<ushort[]>();
        private static readonly ObjectPool<MeshData> _meshDataPool = new ObjectPool<MeshData>();

        private readonly Stopwatch _Stopwatch;
        private readonly INodeCollection<ushort>[] _NeighborBlocksCollections;

        private float3 _OriginPoint;
        private INodeCollection<ushort> _BlocksCollection;
        private MeshData _MeshData;
        private BlockFaces[] _Mask;
        private ushort[] _Blocks;
        private bool _AggressiveFaceMerging;
        private TimeSpan _PreMeshingTimeSpan;
        private TimeSpan _MeshingTimeSpan;

        public ChunkMeshingJob()
        {
            _Stopwatch = new Stopwatch();
            _NeighborBlocksCollections = new INodeCollection<ushort>[6];
        }

        protected override Task Process()
        {
            TimeMeasuredGenerate();

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!CancellationToken.IsCancellationRequested)
            {
                DiagnosticsController.Current.RollingPreMeshingTimes.Enqueue(_PreMeshingTimeSpan);
                DiagnosticsController.Current.RollingMeshingTimes.Enqueue(_MeshingTimeSpan);
            }

            return Task.CompletedTask;
        }


        #region Data

        /// <summary>
        ///     Sets the data required for mesh generation.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation indication.</param>
        /// <param name="originPoint">Origin point of the chunk that's being meshed.</param>
        /// <param name="blocksCollection"><see cref="INodeCollection{T}" /> of blocksCollection contained within the chunk.</param>
        /// <param name="aggressiveFaceMerging">Indicates whether to merge similarly textured faces.</param>
        public void SetData(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocksCollection,
            bool aggressiveFaceMerging)
        {
            CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken, cancellationToken).Token;
            _OriginPoint = originPoint;
            _BlocksCollection = blocksCollection;
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

            _PreMeshingTimeSpan = default;
            _MeshingTimeSpan = default;
        }

        public void ApplyMeshData(ref Mesh mesh) => _MeshData.ApplyMeshData(ref mesh);

        #endregion


        #region Generation

        private void TimeMeasuredGenerate()
        {
            if ((_BlocksCollection == null) || (_BlocksCollection.IsUniform && (_BlocksCollection.Value == BlockController.AirID)))
            {
                return;
            }

            // restart stopwatch to measure pre-mesh op time
            _Stopwatch.Restart();

            PrepareMeshing();

            _Stopwatch.Stop();

            _PreMeshingTimeSpan = _Stopwatch.Elapsed;

            _Stopwatch.Restart();

            GenerateMesh();

            FinishMeshing();

            _Stopwatch.Stop();

            _MeshingTimeSpan = _Stopwatch.Elapsed;
        }

        private void PrepareMeshing()
        {
            // retrieve existing objects from object pool
            _Mask = _masksPool.Retrieve() ?? new BlockFaces[GenerationConstants.CHUNK_SIZE_CUBED];
            _Blocks = _blocksPool.Retrieve() ?? new ushort[GenerationConstants.CHUNK_SIZE_CUBED];
            _MeshData = _meshDataPool.Retrieve() ?? new MeshData(new List<Vector3>(), new List<Vector3>(), new List<int>(), new List<int>());

            // set _BlocksIDs from _BlocksCollection
            _BlocksCollection.CopyTo(_Blocks);

            // unset reference to block collection to avoid use during meshing generation
            _BlocksCollection = null;

            // set block data for relevant neighbor indexes
            foreach ((int3 normal, ChunkController chunkController) in WorldController.Current.GetNeighboringChunksWithNormal(_OriginPoint))
            {
                int neighborIndex = -1;

                if (normal.x != 0)
                {
                    neighborIndex = normal.x > 0 ? 0 : 3;
                }
                else if (normal.y != 0)
                {
                    neighborIndex = normal.y > 0 ? 1 : 4;
                }
                else if (normal.z != 0)
                {
                    neighborIndex = normal.z > 0 ? 2 : 5;
                }

                _NeighborBlocksCollections[neighborIndex] = chunkController.Blocks;
            }
        }

        private void FinishMeshing()
        {
            // clear mask, add to object pool, and unset reference
            Array.Clear(_Mask, 0, _Mask.Length);
            _masksPool.TryAdd(_Mask);
            _Mask = default;

            // clear block ids, add to object pool, and unset reference
            _blocksPool.TryAdd(_Blocks);
            _Blocks = default;

            Array.Clear(_NeighborBlocksCollections, 0, _NeighborBlocksCollections.Length);

            _OriginPoint = default;
            _BlocksCollection = default;
            _AggressiveFaceMerging = default;
        }

        /// <summary>
        ///     Generates the mesh data.
        /// </summary>
        /// <remarks>
        ///     The generated data is stored in the <see cref="MeshData" /> object <see cref="_MeshData" />.
        /// </remarks>
        private void GenerateMesh()
        {
            for (int index = 0; index < _Mask.Length; index++)
            {
                // observe cancellation token
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // set local and global position according to index
                int3 localPosition = WydMath.IndexTo3D(index, GenerationConstants.CHUNK_SIZE);
                int3 globalPosition = WydMath.ToInt(_OriginPoint + localPosition);

                ushort currentBlockId = _Blocks[index];

                if (currentBlockId == BlockController.AirID)
                {
                    continue;
                }

                TraverseIndex(index, globalPosition, localPosition, currentBlockId,
                    BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent));
            }
        }

        private void TraverseIndex(int index, int3 globalPosition, int3 localPosition, ushort currentBlockId, bool transparentTraversal)
        {
            for (int normalIndex = 0; normalIndex < 6; normalIndex++)
            {
                // face direction always exists on a single bit, so offset 1 by the current normalIndex (0-5)
                Direction faceDirection = (Direction)(1 << normalIndex);

                // check if current index has face already
                if (_Mask[index].HasFace(faceDirection))
                {
                    continue;
                }

                // normalIndex constrained to represent the 3 axes
                int iModulo3 = normalIndex % 3;
                // indicates how many times the traversal function has executed for this face direction
                int traversalIterations = 0;
                // total number of successful traversals
                // remark: this is outside the for loop below so that the function can determine if any traversals have happened
                int traversals = 0;

                for (int perpendicularNormalIndex = 1; perpendicularNormalIndex < 3; perpendicularNormalIndex++)
                {
                    // the index of the int3 traversalNormal to traverse on
                    int traversalNormalAxisIndex = (iModulo3 + perpendicularNormalIndex) % 3;
                    // traversal normal, which is a zeroed out int3 with only the traversal axis index set to one
                    int3 traversalNormal = new int3(0)
                    {
                        [traversalNormalAxisIndex] = 1
                    };

                    // current value of the local position by traversal direction
                    int traversalNormalLocalPositionIndexValue = localPosition[traversalNormalAxisIndex];
                    // maximum number of traversals, which is only sliceIndexValue + 1 when not using AggressiveFaceMerging
                    // remark: it's likely that not using AggressiveFaceMerging is much slower since the traversal function still
                    //     runs, but only ever hits once at maximum (thus resulting in a higher total amount of traversing overhead).
                    int maximumTraversals = _AggressiveFaceMerging ? GenerationConstants.CHUNK_SIZE : traversalNormalLocalPositionIndexValue + 1;
                    // amount by integer to add to current index to get 1D projected position of traversal position
                    int traversalIndexStep = GenerationConstants.IndexStepByNormalIndex[traversalNormalAxisIndex];
                    // amount by integer to add to current traversal index to get 1D projected position of facing block
                    int facingBlockIndexStep = GenerationConstants.IndexStepByNormalIndex[normalIndex];

                    // current traversal index, which is increased by traversalIndexStep every iteration the for loop below
                    int traversalIndex = index + (traversals * traversalIndexStep);
                    // current local traversal position, which is increased by TraversalNormal every iteration of the for loop below
                    int3 traversalPosition = localPosition + (traversalNormal * traversals);

                    for (;
                        (traversalNormalLocalPositionIndexValue + traversals) < maximumTraversals;
                        traversals++, // increment traversals
                        traversalIndex += traversalIndexStep, // increment traversal index by index step to adjust local working position
                        traversalPosition += traversalNormal) // increment traversal position by traversal normal to adjust local working position
                    {
                        // check if block's mask already has face set, or if we've traversed at all, ensure block ids match
                        // remark: in the case block ids don't match, we've likely reached textures that won't match.
                        if (_Mask[traversalIndex].HasFace(faceDirection) || ((traversals > 0) && (_Blocks[traversalIndex] != currentBlockId)))
                        {
                            break;
                        }

                        // normal direction of current face
                        int3 faceNormal = GenerationConstants.FaceNormalByIteration[normalIndex];
                        // local position of facing block
                        int3 facingBlockPosition = traversalPosition + faceNormal;
                        // axis value of facing block position
                        int facingPositionAxisValue = facingBlockPosition[iModulo3];

                        ushort facingBlockId;

                        // check if current facing block axis value is within the local chunk
                        if ((facingPositionAxisValue >= 0) && (facingPositionAxisValue <= GenerationConstants.CHUNK_SIZE_MINUS_ONE))
                        {
                            // if so, index into block ids and set facingBlockId
                            facingBlockId = _Blocks[traversalIndex + facingBlockIndexStep];
                        }
                        else
                        {
                            // if not, get local position adjusted to relative position across the chunk boundary
                            int3 boundaryAdjustedLocalPosition = facingBlockPosition + (faceNormal * -GenerationConstants.CHUNK_SIZE_MINUS_ONE);

                            // index into neighbor blocks collections, call .GetPoint() with adjusted local position
                            // remark: if there's no neighbor at the index given, then no chunk exists there (for instance,
                            //     chunks at the edge of render distance). In this case, return NullID so no face is rendered on edges.
                            facingBlockId = _NeighborBlocksCollections[normalIndex]?.GetPoint(boundaryAdjustedLocalPosition)
                                            ?? BlockController.NullID;
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
