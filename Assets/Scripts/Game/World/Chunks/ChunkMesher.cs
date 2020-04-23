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
        private OctreeNode<ushort> _Blocks;
        private bool _AggressiveFaceMerging;
        private List<OctreeNode<ushort>> _NeighborNodes;

        public TimeSpan SetBlockTimeSpan { get; private set; }
        public TimeSpan MeshingTimeSpan { get; private set; }

        public ChunkMesher(CancellationToken cancellationToken, float3 originPoint, OctreeNode<ushort> blocks,
            bool aggressiveFaceMerging)
        {
            if (blocks == null)
            {
                return;
            }

            _Stopwatch = new Stopwatch();
            _MeshData = new MeshData(
                new List<Vector3>(ChunkController.SIZE_CUBED),
                new List<Vector3>(ChunkController.SIZE_CUBED),
                new List<int>(ChunkController.SIZE_CUBED), // triangles
                new List<int>(ChunkController.SIZE_CUBED)); // transparent triangles
            _Mask = new BlockFaces[ChunkController.SIZE_CUBED];

            PrepareMeshing(cancellationToken, originPoint, blocks, aggressiveFaceMerging);
        }


        #region Runtime

        public void PrepareMeshing(CancellationToken cancellationToken, float3 originPoint, OctreeNode<ushort> blocks,
            bool aggressiveFaceMerging)
        {
            _CancellationToken = cancellationToken;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveFaceMerging;

            _MeshData.Clear();
        }

        public void Reset()
        {
            _MeshData.Clear();
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
                _NeighborNodes = new List<OctreeNode<ushort>>(6);
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

                ushort currentBlockId = _Blocks.GetPoint(globalPosition, true);

                if (currentBlockId == BlockController.AirID)
                {
                    continue;
                }

                if (BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent, false))
                {
                    //TraverseIndexTransparent(WydMath.ToInt(_OriginPoint), index, globalPosition, localPosition);
                }
                else
                {
                    TraverseIndex(index, globalPosition, localPosition, currentBlockId);
                }
            }

            _Stopwatch.Stop();
            MeshingTimeSpan = _Stopwatch.Elapsed;
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


        #region TRAVERSAL MESHING

        private void TraverseIndex(int index, int3 globalPosition, int3 localPosition, ushort currentBlockId)
        {
            // iterates positive axis (1) and then negative axis (-1)
            for (int sign = 1; sign > -2; sign--)
            {
                if (sign == 0)
                {
                    // skip zeroed out sign
                    continue;
                }

                // iterates each axis
                for (int i = 0; i < 3; i++)
                {
                    int3 faceNormal = new int3
                    {
                        [i] = sign
                    };
                    Direction faceDirection = Directions.NormalToDirection(faceNormal);

                    if (_Mask[index].HasFace(faceDirection))
                    {
                        continue;
                    }

                    if ( // check if local position is at edge of chunk, and if so check face direction from neighbor
                        ((((sign > 0) && (localPosition[i] == (ChunkController.SIZE - 1))) || ((sign < 0) && (localPosition[i] == 0)))
                         && BlockController.Current.CheckBlockHasProperty(GetNeighboringBlock(faceNormal, globalPosition + faceNormal),
                             BlockDefinition.Property.Transparent, false))
                        // local position is inside chunk, so retrieve from blocks
                        || BlockController.Current.CheckBlockHasProperty(_Blocks.GetPoint(globalPosition + faceNormal, true),
                            BlockDefinition.Property.Transparent, false))
                    {
                        _Mask[index].SetFace(faceDirection, true);
                        AddTriangles(faceDirection);

                        float2 uvSize = new float2(1f);

                        if (_AggressiveFaceMerging)
                        {
                            int traversals = 0;
                            int uvIndex = 0;
                            float3 finalTraversalNormal = float3.zero;

                            foreach ((int traversalNormalIndex, int3 traversalNormal) in GetTraversalNormals(faceNormal))
                            {
                                int traversalNormalIndexAdjusted = 0;

                                // so when traversalNormalIndex == 1, we're iterating the Up direction, which is
                                // SIZE ^ 2. Thus, there's an alignment issue with the index step and
                                // the value that traversalNormalIndex returns from the indexStep equation below.
                                //
                                // This logic is for adjusting traversalNormalIndex to match the normal direction.
                                switch (traversalNormalIndex)
                                {
                                    case 1:
                                        traversalNormalIndexAdjusted = 2;
                                        break;
                                    case 2:
                                        traversalNormalIndexAdjusted = 1;
                                        break;
                                }

                                float indexStepUnclamped = math.pow(ChunkController.SIZE, traversalNormalIndexAdjusted);
                                float indexStep = indexStepUnclamped == 0f ? 1f : indexStepUnclamped;
                                int indexStepSigned = (int)indexStep;

                                traversals = GetTraversals(index, globalPosition, localPosition[traversalNormalIndex], traversalNormal, faceNormal,
                                    faceDirection, indexStepSigned, false);

                                finalTraversalNormal = traversalNormal;

                                if (traversals > 1)
                                {
                                    //uvSize += (traversals + (math.cross(traversalNormal, faceNormal)));
                                    uvSize[uvIndex] = traversals;
                                    break;
                                }

                                uvIndex += 1;
                            }

                            for (int vert = 0; vert < 4; vert++)
                            {
                                float3 traversalVertex = BlockFaces.Vertices.FaceVertices[faceDirection][vert]
                                                         * math.clamp(traversals * finalTraversalNormal, 1, int.MaxValue);
                                _MeshData.AddVertex(localPosition + traversalVertex);
                            }
                        }
                        else
                        {
                            AddVertices(faceDirection, localPosition);
                        }

                        if (BlockController.Current.GetUVs(currentBlockId, globalPosition, /* todo fix that -> */ Direction.North, uvSize,
                            out BlockUVs blockUVs))
                        {
                            _MeshData.AddUV(blockUVs.TopLeft);
                            _MeshData.AddUV(blockUVs.TopRight);
                            _MeshData.AddUV(blockUVs.BottomLeft);
                            _MeshData.AddUV(blockUVs.BottomRight);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Gets the total amount of possible traversals for face merging in a direction
        /// </summary>
        /// <param name="index">1D index of current block.</param>
        /// <param name="globalPosition">Global position of starting block.</param>
        /// <param name="slice">Current slice (x, y, or z) of a 3D index relative to your traversal direction.</param>
        /// <param name="traversalNormal">Direction to traverse in.</param>
        /// <param name="faceNormal">Direction to check faces while traversing.</param>
        /// <param name="traversalFactor">Amount of indexes to move forwards for each successful traversal in given direction.</param>
        /// <param name="transparentTraversal">Determines whether or not transparent traversal will be used.</param>
        /// <returns><see cref="int" /> representing how many successful traversals were made in the given traversal direction.</returns>
        private int GetTraversals(int index, int3 globalPosition, int slice, int3 traversalNormal, int3 faceNormal, Direction faceDirection,
            int traversalFactor, bool transparentTraversal)
        {
            if (!_AggressiveFaceMerging)
            {
                return 1;
            }

            ushort currentId = _Blocks.GetPoint(globalPosition, true);

            int traversals;

            for (traversals = 1; (slice + traversals) < ChunkController.SIZE; traversals++)
            {
                // incrementing on x, so the traversal factor is 1
                // if we were incrementing on z, the factor would be ChunkController.Size3D.x
                // and on y it would be (ChunkController.Size3D.x * ChunkController.Size3D.z)
                int traversalIndex = index + (traversals * traversalFactor);
                float3 currentTraversalPosition = globalPosition + (traversals * traversalNormal);

                if ((_Blocks.GetPoint(currentTraversalPosition, true) != currentId)
                    || _Mask[traversalIndex].HasFace(Directions.NormalToDirection(faceNormal)))
                {
                    break;
                }

                float3 traversalFacingBlockPosition = currentTraversalPosition + faceNormal;
                float3 traversalLengthFromOrigin = traversalFacingBlockPosition - _OriginPoint;
                ushort facingBlockId;

                if (math.all(traversalLengthFromOrigin >= 0) && math.all(traversalLengthFromOrigin <= (ChunkController.SIZE - 1)))
                {
                    // coordinates are inside, so retrieve from own blocks octree
                    facingBlockId = _Blocks.GetPoint(traversalFacingBlockPosition, true);
                }
                else
                {
                    facingBlockId = GetNeighboringBlock(faceNormal, traversalFacingBlockPosition);
                }

                // if transparent, traverse as long as block is the same
                // if opaque, traverse as long as faceNormal-adjacent block is transparent
                if ((transparentTraversal && (currentId != facingBlockId))
                    || !BlockController.Current.CheckBlockHasProperty(facingBlockId, BlockDefinition.Property.Transparent, false))
                {
                    break;
                }

                // set face to traversed and continue traversal
                _Mask[traversalIndex].SetFace(faceDirection, true);
            }

            return traversals;
        }

        #endregion


        #region Helper Methods

        // todo this seems dumb
        private static IEnumerable<(int, int3)> GetTraversalNormals(float3 normal)
        {
            for (int normalIndex = 0; normalIndex < 3; normalIndex++)
            {
                if (normal[normalIndex] == 0f)
                {
                    yield return (normalIndex, new int3
                    {
                        [normalIndex] = 1
                    });
                }
            }
        }

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
        private ushort GetNeighboringBlock(int3 normal, float3 globalPosition)
        {
            int index = GetNeighborIndexFromNormal(normal);

            // if neighbor chunk doesn't exist, then return true (to mean, return blockId == NullID
            // otherwise, query octree for target neighbor and return block id
            return _NeighborNodes[index] == default ? BlockController.NullID : _NeighborNodes[index].GetPoint(globalPosition, true);
        }

        #endregion
    }
}
