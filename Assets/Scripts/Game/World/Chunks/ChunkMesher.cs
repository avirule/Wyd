#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.Game.World.Blocks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Graphics;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMesher
    {
        private readonly Stopwatch _Stopwatch;
        private readonly BlockFaces[] _Mask;
        private readonly MeshData _MeshData;

        private CancellationToken _CancellationToken;
        private float3 _OriginPoint;
        private bool _AggressiveFaceMerging;
        private INodeCollection<ushort> _Blocks;
        private List<INodeCollection<ushort>> _NeighborNodes;

        public TimeSpan SetBlockTimeSpan { get; private set; }
        public TimeSpan MeshingTimeSpan { get; private set; }

        public ChunkMesher(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocks, bool aggressiveFaceMerging)
        {
            if (blocks == null)
            {
                return;
            }

            _Stopwatch = new Stopwatch();
            _MeshData = new MeshData(new List<Vector3>(), new List<Vector3>(),
                new List<int>(), // triangles
                new List<int>()); // transparent triangles
            _Mask = new BlockFaces[ChunkController.SIZE_CUBED];

            PrepareMeshing(cancellationToken, originPoint, blocks, aggressiveFaceMerging);
        }


        #region Runtime

        public void PrepareMeshing(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocks,
            bool aggressiveFaceMerging)
        {
            _CancellationToken = cancellationToken;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveFaceMerging;

            _MeshData.Clear(true);
        }

        public void Reset()
        {
            _MeshData.Clear(true);
            _Blocks = null;
        }

        public void ApplyMeshData(ref Mesh mesh) => _MeshData.ApplyMeshData(ref mesh);

        public void GenerateMesh()
        {
            if ((_Blocks == null) || (_Blocks.IsUniform && (_Blocks.Value == BlockController.AirID)))
            {
                return;
            }

            _Stopwatch.Restart();

            if (_NeighborNodes == null)
            {
                _NeighborNodes = new List<INodeCollection<ushort>>(6);
            }

            _NeighborNodes.Clear();

            for (int i = 0; i < 6; i++)
            {
                _NeighborNodes.Add(null);
            }

            // todo provide this data from ChunkMeshController maybe?
            foreach ((int3 normal, ChunkController chunkController) in WorldController.Current.GetNeighboringChunksWithNormal(_OriginPoint))
            {
                _NeighborNodes[GetNeighborIndexFromNormal(normal)] = chunkController.Blocks;
            }

            // clear masks for new meshing
            Array.Clear(_Mask, 0, _Mask.Length);

            _Stopwatch.Stop();

            SetBlockTimeSpan = _Stopwatch.Elapsed;

            _Stopwatch.Restart();

            for (int index = 0; index < _Mask.Length; index++)
            {
                if (_CancellationToken.IsCancellationRequested)
                {
                    return;
                }

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

            _Stopwatch.Stop();
            MeshingTimeSpan = _Stopwatch.Elapsed;
        }

        #endregion


        #region Traversal Meshing

        private void TraverseIndex(int index, int3 globalPosition, int3 localPosition, ushort currentBlockId, bool transparentTraversal)
        {
            for (int i = 0; i < 6; i++)
            {
                int3 faceNormal = GenerationConstants.FaceNormalByIteration[i];
                Direction faceDirection = GenerationConstants.FaceDirectionByIteration[i];

                if (_Mask[index].HasFace(faceDirection))
                {
                    continue;
                }

                int iModulo3 = i % 3;
                int moduloAxis = localPosition[iModulo3];

                if (((i <= 2) && (moduloAxis >= (ChunkController.SIZE - 1))) || ((i > 2) && (moduloAxis <= 0)))
                {
                    ushort facingBlockId = GetNeighboringBlock(faceNormal, localPosition);

                    if ((transparentTraversal && (currentBlockId == facingBlockId))
                        || !BlockController.Current.CheckBlockHasProperty(facingBlockId, BlockDefinition.Property.Transparent))
                    {
                        continue;
                    }
                }
                else
                {
                    ushort facingBlockId = _Blocks.GetPoint(localPosition + faceNormal);

                    if ((transparentTraversal && (currentBlockId == facingBlockId))
                        || !BlockController.Current.CheckBlockHasProperty(facingBlockId, BlockDefinition.Property.Transparent))
                    {
                        continue;
                    }
                }

                _Mask[index].SetFace(faceDirection);
                AddTriangles(faceDirection, transparentTraversal);

                float2 uvSize = new float2(1f);

                if (_AggressiveFaceMerging)
                {
                    int traversals = 0;
                    float3 traversalNormal = float3.zero;

                    foreach ((int traversalNormalIndex, int3 currentTraversalNormal) in GenerationConstants.PerpendicularNormals[iModulo3])
                    {
                        traversals = GetTraversals(index, localPosition, traversalNormalIndex, currentTraversalNormal, faceNormal,
                            faceDirection, GenerationConstants.IndexStepByTraversalNormalIndex[traversalNormalIndex], transparentTraversal);

                        traversalNormal = currentTraversalNormal;

                        if (traversals <= 1)
                        {
                            continue;
                        }

                        uvSize[GenerationConstants.UVIndexAdjustments[iModulo3][traversalNormalIndex]] = traversals;
                        break;
                    }

                    float3 traversalVertexOffset = math.max(traversals * traversalNormal, 1);

                    for (int vert = 0; vert < 4; vert++)
                    {
                        float3 traversalVertex = BlockFaces.Vertices.FaceVertices[faceDirection][vert] * traversalVertexOffset;
                        _MeshData.AddVertex(localPosition + traversalVertex);
                    }
                }
                else
                {
                    AddVertices(faceDirection, localPosition);
                }

                if (!BlockController.Current.GetUVs(currentBlockId, globalPosition, faceDirection, uvSize, out BlockUVs blockUVs))
                {
                    continue;
                }

                _MeshData.AddUV(blockUVs.TopLeft);
                _MeshData.AddUV(blockUVs.TopRight);
                _MeshData.AddUV(blockUVs.BottomLeft);
                _MeshData.AddUV(blockUVs.BottomRight);
            }
        }

        /// <summary>
        ///     Gets the total amount of possible traversals for face merging in a direction
        /// </summary>
        /// <param name="index">1D index of current block.</param>
        /// <param name="localPosition"></param>
        /// <param name="sliceIndex">Current sliceIndex (x, y, or z) of a 3D index relative to your traversal direction.</param>
        /// <param name="traversalNormal">Direction to traverse in.</param>
        /// <param name="faceNormal">Direction to check faces while traversing.</param>
        /// <param name="faceDirection"></param>
        /// <param name="traversalFactor">Amount of indexes to move forwards for each successful traversal in given direction.</param>
        /// <param name="transparentTraversal">Determines whether or not transparent traversal will be used.</param>
        /// <returns><see cref="int" /> representing how many successful traversals were made in the given traversal direction.</returns>
        private int GetTraversals(int index, float3 localPosition, int sliceIndex, int3 traversalNormal, int3 faceNormal, Direction faceDirection,
            int traversalFactor, bool transparentTraversal)
        {
            if (!_AggressiveFaceMerging)
            {
                return 1;
            }

            ushort initialBlockId = _Blocks.GetPoint(localPosition);

            int traversals;
            float sliceIndexValue = localPosition[sliceIndex];

            for (traversals = 1; (sliceIndexValue + traversals) < ChunkController.SIZE; traversals++)
            {
                // incrementing on x, so the traversal factor is 1
                // if we were incrementing on z, the factor would be ChunkController.Size3D.x
                // and on y it would be (ChunkController.Size3D.x * ChunkController.Size3D.z)
                int traversalIndex = index + (traversals * traversalFactor);
                float3 currentTraversalPosition = localPosition + (traversalNormal * traversals);

                if ((_Blocks.GetPoint(currentTraversalPosition) != initialBlockId) || _Mask[traversalIndex].HasFace(faceDirection))
                {
                    break;
                }

                float3 traversalFacingBlockPosition = currentTraversalPosition + faceNormal;
                ushort facingBlockId;

                if (traversalFacingBlockPosition[sliceIndex] >= 0 && traversalFacingBlockPosition[sliceIndex] <= (ChunkController.SIZE - 1))
                {
                    // coordinates are inside, so retrieve from own blocks octree
                    facingBlockId = _Blocks.GetPoint(traversalFacingBlockPosition);
                }
                else
                {
                    facingBlockId = GetNeighboringBlock(faceNormal, traversalFacingBlockPosition);
                }

                // if transparent, traverse as long as block is the same
                // if opaque, traverse as long as faceNormal-adjacent block is transparent
                if ((transparentTraversal && (initialBlockId != facingBlockId))
                    || !BlockController.Current.CheckBlockHasProperty(facingBlockId, BlockDefinition.Property.Transparent))
                {
                    break;
                }

                // set face to traversed and continue traversal
                _Mask[traversalIndex].SetFace(faceDirection);
            }

            return traversals;
        }

        #endregion


        #region Add Verts/Tris

        private void AddVertices(Direction direction, int3 localPosition)
        {
            foreach (float3 vertex in BlockFaces.Vertices.FaceVertices[direction])
            {
                _MeshData.AddVertex(vertex + localPosition);
            }
        }

        private void AddTriangles(Direction direction, bool transparent = false)
        {
            if (transparent)
            {
                _MeshData.AddTriangles(1, BlockFaces.Triangles.FaceTriangles[direction]
                    .Select(triangle => triangle + _MeshData.Vertices.Count));
            }
            else
            {
                _MeshData.AddTriangles(0, BlockFaces.Triangles.FaceTriangles[direction]
                    .Select(triangle => triangle + _MeshData.Vertices.Count));
            }
        }

        #endregion


        #region Helper Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetNeighborIndexFromNormal(int3 normal)
        {
            int index = -1;

            // chunk index by normal value (from -1 to 1 on each axis):
            // positive: 1    4    0
            // negative: 3    5    2

            if (normal.x != 0)
            {
                index = normal.x > 0 ? 0 : 1;
            }
            else if (normal.y != 0)
            {
                index = normal.y > 0 ? 2 : 3;
            }
            else if (normal.z != 0)
            {
                index = normal.z > 0 ? 4 : 5;
            }

            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetNeighboringBlock(int3 normal, float3 localPosition)
        {
            int index = GetNeighborIndexFromNormal(normal);

            // if neighbor chunk doesn't exist, then return true (to mean, return blockId == NullID
            // otherwise, query octree for target neighbor and return block id
            return _NeighborNodes[index] != null
                ? _NeighborNodes[index].GetPoint(math.abs(localPosition + (-normal * (ChunkController.SIZE - 1))))
                : BlockController.NullID;
        }

        #endregion
    }
}
