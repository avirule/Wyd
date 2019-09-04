#region

using System;
using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using Game;
using Game.World.Blocks;
using Game.World.Chunks;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Threading
{
    public class ChunkMeshingThreadedItem : ThreadedItem
    {
        private readonly List<int> _Triangles;
        private readonly List<Vector3> _Vertices;
        private readonly List<Vector3> _UVs;
        private readonly byte[] _Faces;

        private Vector3 _Position;
        private Block[] _Blocks;
        private bool _AggressiveFaceMerging;

        /// <summary>
        ///     Initialises a new instance of the <see cref="ChunkMeshingThreadedItem" /> class.
        /// </summary>
        /// <seealso cref="ChunkBuildingThreadedItem" />
        public ChunkMeshingThreadedItem()
        {
            _Triangles = new List<int>();
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Faces = new byte[Chunk.Size.Product()];
        }

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="aggressiveLinking"></param>
        public void Set(Vector3 position, Block[] blocks, bool aggressiveLinking)
        {
            _Vertices.Clear();
            _Triangles.Clear();
            _UVs.Clear();

            _Position = position;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveLinking;
        }

        protected override void Process()
        {
            if (_Blocks == default)
            {
                return;
            }

            GenerateTraversedMeshData();
        }

        /// <summary>
        ///     Applies and returns processed <see cref="UnityEngine.Mesh" />.
        /// </summary>
        /// <param name="mesh">Given <see cref="UnityEngine.Mesh" /> to apply processed data to.</param>
        /// <returns>Processed <see cref="UnityEngine.Mesh" />.</returns>
        public void SetMesh(ref Mesh mesh)
        {
            if ((_Vertices.Count == 0) ||
                (_Triangles.Count == 0))
            {
                return;
            }

            if (mesh == default)
            {
                mesh = new Mesh();
            }
            else
            {
                mesh.Clear();
            }

            if (_Vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(_Vertices);
            mesh.SetTriangles(_Triangles, 0);

            // in case of no UVs to apply to mesh
            if (_UVs.Count > 0)
            {
                mesh.SetUVs(0, _UVs);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        #region NAIVE MESHING

        private void GenerateTraversedMeshData()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
                if (AbortToken.IsCancellationRequested)
                {
                    return;
                }

                if (_Blocks[index].Id == BlockController.BLOCK_EMPTY_ID)
                {
                    continue;
                }

                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);
                Vector3 localPosition = new Vector3(x, y, z);
                Vector3 globalPosition = _Position + localPosition;


                if ((((z == (Chunk.Size.z - 1)) &&
                      WorldController.Current.GetBlockAt(globalPosition + Vector3.forward).Transparent) ||
                     ((z < (Chunk.Size.z - 1)) && _Blocks[index + Chunk.Size.x].Transparent)) &&
                    !_Blocks[index].HasFace(Direction.North))
                {
                    _Blocks[index].SetFace(Direction.North, true);
                    AddTriangles(Direction.North);

                    int traversals = 1;

                    if (_AggressiveFaceMerging)
                    {
                        traversals = GetTraversals(index, globalPosition, x, Direction.North);
                        // The traversals value guess into the vertex points that have a positive value
                        // on the same axis as your slice value
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                    }
                    else
                    {
                        AddVertices(Direction.North, localPosition);
                    }

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.North, new Vector3(traversals, 0f, 1f), out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                    }
                }

                if ((((x == (Chunk.Size.x - 1)) &&
                      WorldController.Current.GetBlockAt(globalPosition + Vector3.right).Transparent) ||
                     ((x < (Chunk.Size.x - 1)) && _Blocks[index + 1].Transparent)) &&
                    !_Blocks[index].HasFace(Direction.East))
                {
                    _Blocks[index].SetFace(Direction.East, true);
                    AddTriangles(Direction.East);

                    int traversals = 1;
                    if (_AggressiveFaceMerging)
                    {
                        traversals = GetTraversals(index, globalPosition, z, Direction.East);
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                    }
                    else
                    {
                        AddVertices(Direction.East, localPosition);
                    }

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.East, new Vector3(traversals, 0f, 1f), out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if ((((z == 0) && WorldController.Current.GetBlockAt(globalPosition + Vector3.back).Transparent) ||
                     ((z > 0) && _Blocks[index - Chunk.Size.x].Transparent)) &&
                    !_Blocks[index].HasFace(Direction.South))
                {
                    _Blocks[index].SetFace(Direction.South, true);
                    AddTriangles(Direction.South);

                    int traversals = 1;
                    if (_AggressiveFaceMerging)
                    {
                        traversals = GetTraversals(index, globalPosition, x, Direction.South);
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                    }
                    else
                    {
                        AddVertices(Direction.South, localPosition);
                    }

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.South, new Vector3(traversals, 0f, 1f), out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if ((((x == 0) && WorldController.Current.GetBlockAt(globalPosition + Vector3.left).Transparent) ||
                     ((x > 0) && _Blocks[index - 1].Transparent)) &&
                    !_Blocks[index].HasFace(Direction.West))
                {
                    _Blocks[index].SetFace(Direction.West, true);
                    AddTriangles(Direction.West);

                    int traversals = 1;
                    if (_AggressiveFaceMerging)
                    {
                        traversals = GetTraversals(index, globalPosition, z, Direction.West);
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                    }
                    else
                    {
                        AddVertices(Direction.West, localPosition);
                    }

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.West, new Vector3(traversals, 0f, 1f), out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                    }
                }

                if ((((y == (Chunk.Size.y - 1)) &&
                      WorldController.Current.GetBlockAt(globalPosition + Vector3.up).Transparent) ||
                     ((y < (Chunk.Size.y - 1)) && _Blocks[index + (Chunk.Size.x * Chunk.Size.z)].Transparent)) &&
                    !_Blocks[index].HasFace(Direction.Up))
                {
                    _Blocks[index].SetFace(Direction.Up, true);
                    AddTriangles(Direction.Up);

                    int traversals = 1;
                    if (_AggressiveFaceMerging)
                    {
                        traversals = GetTraversals(index, globalPosition, z, Direction.Up);
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                    }
                    else
                    {
                        AddVertices(Direction.Up, localPosition);
                    }

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.Up, new Vector3(traversals, 0f, 1f), out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                    }
                }

                // ignore the very bottom face of the world to reduce verts/tris
                if ((y > 0) && _Blocks[index - (Chunk.Size.x * Chunk.Size.z)].Transparent &&
                    !_Blocks[index].HasFace(Direction.Down))
                {
                    _Blocks[index].SetFace(Direction.Down, true);
                    AddTriangles(Direction.Down);

                    int traversals = 1;
                    if (_AggressiveFaceMerging)
                    {
                        traversals = GetTraversals(index, globalPosition, z, Direction.Down);
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                    }
                    else
                    {
                        AddVertices(Direction.Down, localPosition);
                    }

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.Down, new Vector3(traversals, 0f, 1f), out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }
            }
        }

        private void AddTriangles(Direction direction)
        {
            int[] triangles = BlockFaces.Triangles.Get(direction);

            foreach (int triangleValue in triangles)
            {
                _Triangles.Add(_Vertices.Count + triangleValue);
            }
        }

        private void AddVertices(Direction direction, Vector3 localPosition)
        {
            Vector3[] vertices = BlockFaces.Vertices.Get(direction);

            foreach (Vector3 vertex in vertices)
            {
                _Vertices.Add(vertex + localPosition);
            }
        }

        /// <summary>
        ///     Gets the total amount of possible traversals for face merging in a direction
        /// </summary>
        /// <param name="index">1D index of current block</param>
        /// <param name="globalPosition">Global position of current block</param>
        /// <param name="slice">Current slice perpendicular to the traversing direction</param>
        /// <param name="direction">Direction to traverse</param>
        /// <returns></returns>
        private int GetTraversals(int index, Vector3 globalPosition, int slice, Direction direction)
        {
            // 1 being the current block at `index`
            int traversals = 1;

            // todo make aggressive linking compatible with special block shapes
            if (!_AggressiveFaceMerging)
            {
                return traversals;
            }

            int traversalFactor = GetTraversalFactor(direction);
            int limitingSliceValue = GetLimitingSliceValue(direction);
            Vector3 traversalDirectionAsVector3 = GetTraversalDirectionAsVector3(direction);

            // incrementing on x, so the traversal factor is 1
            // if we were incrementing on z, the factor would be Chunk.Size.x
            // and on y it would be (Chunk.Size.x * Chunk.Size.z)
            int traversalIndex = index + (traversals * traversalFactor);

            while ( // Set traversalIndex and ensure it is within the chunk's context
                ((slice + traversals) < limitingSliceValue) &&
                // This check removes the need to check if the adjacent block is transparent,
                // as our current block will never be transparent
                (_Blocks[index].Id == _Blocks[traversalIndex].Id) &&
                !_Blocks[traversalIndex].HasFace(direction) &&
                // ensure the block to the north of our current block is transparent
                WorldController.Current.GetBlockAt(
                        globalPosition + (traversals * traversalDirectionAsVector3) + direction.AsVector3())
                    .Transparent)
            {
                _Blocks[traversalIndex].SetFace(direction, true);

                // increment and set traversal values
                traversals++;
                traversalIndex = index + (traversals * traversalFactor);
            }

            return traversals;
        }

        private int GetTraversalFactor(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                case Direction.South:
                    return 1;
                case Direction.East:
                case Direction.West:
                    return Chunk.Size.x;
                case Direction.Up:
                case Direction.Down:
                    return Chunk.Size.z;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private int GetLimitingSliceValue(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                case Direction.South:
                    return Chunk.Size.x;
                case Direction.East:
                case Direction.West:
                    return Chunk.Size.z;
                case Direction.Up:
                case Direction.Down:
                    return Chunk.Size.z;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private Vector3 GetTraversalDirectionAsVector3(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                case Direction.South:
                    return Vector3.right;
                case Direction.East:
                case Direction.West:
                    return Vector3.forward;
                case Direction.Up:
                case Direction.Down:
                    return Vector3.forward;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        #endregion

        #region GREEDY MESHING

        private void GenerateGreedyMeshData()
        {
            // This greedy algorithm is converted from PHP to C# from this article:
            // http://0fps.wordpress.com/2012/06/30/meshing-in-a-minecraft-game/
            //
            // The original source code can be found here:
            // https://github.com/mikolalysenko/mikolalysenko.github.com/blob/gh-pages/MinecraftMeshes/js/greedy.js

            // roll sizes into array for simpler relation when processing axes 
            int[] sizes = Chunk.Size.ToArray();

            for (int axis = 0; axis < 3; axis++)
            {
                int i, j, k, l;
                int width, height;
                int axisIncrementedOne = (axis + 1) % 3;
                int axisIncrementedTwo = (axis + 2) % 3;
                int[] x = new int[3];
                int[] q = new int[3];

                // initialise largest possible mask, which is y * y
                bool[] mask = new bool[Chunk.Size.y * Chunk.Size.y];

                q[axis] = 1;

                for (x[axis] = -1; x[axis] < sizes[axis];)
                {
                    // Compute the mask
                    int n = 0;
                    for (x[axisIncrementedTwo] = 0;
                        x[axisIncrementedTwo] < sizes[axisIncrementedTwo];
                        ++x[axisIncrementedTwo])
                    {
                        for (x[axisIncrementedOne] = 0;
                            x[axisIncrementedOne] < sizes[axisIncrementedOne];
                            ++x[axisIncrementedOne])
                        {
                            bool maskValue;

                            Vector3 blockAPosition = new Vector3(x[0], x[1], x[2]);
                            Vector3 blockBPosition = new Vector3(x[0] + q[0], x[1] + q[1], x[2] + q[2]);

                            ushort blockAId =
                                0 <= x[axis]
                                    ? _Blocks[blockAPosition.To1D(Chunk.Size)].Id
                                    : BlockController.BLOCK_EMPTY_ID;

                            ushort blockBId = x[axis] < (sizes[axis] - 1)
                                ? _Blocks[blockBPosition.To1D(Chunk.Size)].Id
                                : WorldController.Current.GetBlockAt(blockBPosition).Id;

                            bool blockAIsTransparent = !BlockController.Current.IsBlockDefaultTransparent(blockAId);
                            bool blockBIsTransparent = !BlockController.Current.IsBlockDefaultTransparent(blockBId);

                            // decide if a face should be drawn based on whether two neighbors are opposite transparency
                            // and also if the block types match
                            maskValue = (blockAIsTransparent != blockBIsTransparent) && (blockAId != blockBId);
                            mask[n++] = maskValue;
                        }
                    }

                    // Increment x[d]
                    x[axis]++;

                    // Generate mesh for mask using lexicographic ordering
                    n = 0;
                    for (j = 0; j < sizes[axisIncrementedTwo]; ++j)
                    {
                        for (i = 0; i < sizes[axisIncrementedOne];)
                        {
                            if (mask[n])
                            {
                                // Compute width
                                for (width = 1; ((i + width) < sizes[axisIncrementedOne]) && mask[n + width]; ++width)
                                {
                                }

                                // Compute height (this is slightly awkward
                                bool done = false;
                                for (height = 1; (j + height) < sizes[axisIncrementedTwo]; ++height)
                                {
                                    for (k = 0; k < width; ++k)
                                    {
                                        if (mask[n + k + (height * sizes[axisIncrementedTwo])])
                                        {
                                            continue;
                                        }

                                        done = true;
                                        break;
                                    }

                                    if (done)
                                    {
                                        break;
                                    }
                                }

                                // Add quad
                                x[axisIncrementedOne] = i;
                                x[axisIncrementedTwo] = j;
                                int[] du = new int[3];
                                int[] dv = new int[3];
                                du[axisIncrementedOne] = width;
                                dv[axisIncrementedTwo] = height;

                                AddFace(
                                    new Vector3(x[0], x[1], x[2]),
                                    new Vector3(x[0] + du[0], x[1] + du[1], x[2] + du[2]),
                                    new Vector3(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]),
                                    new Vector3(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]));


                                // Zero-out mask
                                for (l = 0; l < height; ++l)
                                {
                                    for (k = 0; k < width; ++k)
                                    {
                                        mask[n + k + (l * sizes[axis])] = false;
                                    }
                                }

                                // Increment counters and continue
                                i += width;
                                n += width;
                            }
                            else
                            {
                                i++;
                                n++;
                            }
                        }
                    }
                }
            }
        }

        private void AddFace(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
        {
            _Triangles.Add(_Vertices.Count + 1);
            _Triangles.Add(_Vertices.Count + 1);
            _Triangles.Add(_Vertices.Count + 1);
            _Triangles.Add(_Vertices.Count + 1);
            _Triangles.Add(_Vertices.Count + 1);
            _Triangles.Add(_Vertices.Count + 1);

            _Vertices.Add(v1);
            _Vertices.Add(v2);
            _Vertices.Add(v3);
            _Vertices.Add(v4);
        }

        #endregion
    }
}