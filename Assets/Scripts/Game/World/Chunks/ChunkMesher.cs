#region

using System.Collections.Generic;
using System.Threading;
using Controllers.State;
using Controllers.World;
using Game.World.Blocks;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable TooWideLocalVariableScope

#endregion

namespace Game.World.Chunks
{
    public class ChunkMesher
    {
        private readonly List<int> _Triangles;
        private readonly List<Vector3> _Vertices;
        private readonly List<Vector3> _UVs;

        public CancellationToken AbortToken;
        public Vector3 Position;
        public Block[] Blocks;
        public bool AggressiveFaceMerging;

        public ChunkMesher()
        {
            _Triangles = new List<int>();
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
        }

        /// <summary>
        ///     Initialises a new instance of the <see cref="ChunkMeshingThreadedItem" /> class.
        /// </summary>
        /// <seealso cref="ChunkBuildingThreadedItem" />
        public ChunkMesher(
            Vector3 position, Block[] blocks, bool aggressiveFaceMerging,
            CancellationToken abortToken) : this()
        {
            AbortToken = abortToken;
            Position.Set(position.x, position.y, position.z);
            Blocks = blocks;
            AggressiveFaceMerging = aggressiveFaceMerging;
        }

        public void ClearInternalData()
        {
            _Vertices.Clear();
            _Triangles.Clear();
            _UVs.Clear();
        }

        public void GenerateMesh()
        {
            for (int index = 0; (index < Blocks.Length) && !AbortToken.IsCancellationRequested; index++)
            {
                TraverseIndex(index);
            }
        }

        /// <summary>
        ///     Applies and returns processed <see cref="UnityEngine.Mesh" />.
        /// </summary>
        /// <param name="mesh">Given <see cref="UnityEngine.Mesh" /> to apply processed data to.</param>
        /// <returns>Processed <see cref="UnityEngine.Mesh" />.</returns>
        public void SetMesh(ref Mesh mesh)
        {
            if ((_Vertices.Count == 0) || (_Triangles.Count == 0))
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

            mesh.indexFormat = _Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;

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

        #region SIMPLER MESHING

        private void TraverseIndex(int index)
        {
            if (Blocks[index].Id == BlockController.BLOCK_EMPTY_ID)
            {
                return;
            }

            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, ChunkController.Size);
            Vector3 localPosition = new Vector3(x, y, z);
            Vector3 globalPosition = Position + localPosition;

            if (
                // check if we're on the far edge of the chunk, and if so, query WorldController for blocks in adjacent chunk
                (((z == (ChunkController.Size.z - 1))
                  && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.forward, out Block block)
                  && block.Transparent)
                 // however if we're inside the chunk, use the proper Blocks[] array index for check 
                 || ((z < (ChunkController.Size.z - 1)) && Blocks[index + ChunkController.Size.x].Transparent))
                // ensure this block hasn't already been traversed
                && !Blocks[index].HasFace(Direction.North))
            {
                // set face of current block so it isn't traversed over
                Blocks[index].SetFace(Direction.North, true);
                // add triangles for this block face
                AddTriangles(Direction.North);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, x, Direction.North, Direction.East, 1,
                        ChunkController.Size.x);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, y, Direction.North, Direction.Up,
                            ChunkController.YIndexStep, ChunkController.Size.y);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    // no traversals found, so just mesh a regular 1x1 face
                    AddVertices(Direction.North, localPosition);
                }

                // attempt to retrieve and add uvs for block face
                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.North, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if ((((x == (ChunkController.Size.x - 1))
                  && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out block)
                  && block.Transparent)
                 || ((x < (ChunkController.Size.x - 1)) && Blocks[index + 1].Transparent))
                && !Blocks[index].HasFace(Direction.East))
            {
                Blocks[index].SetFace(Direction.East, true);
                AddTriangles(Direction.East);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, z, Direction.East, Direction.North,
                        ChunkController.Size.x, ChunkController.Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, y, Direction.East, Direction.Up,
                            ChunkController.YIndexStep, ChunkController.Size.y);

                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.East, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.East, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if ((((z == 0)
                  && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out block)
                  && block.Transparent)
                 || ((z > 0) && Blocks[index - ChunkController.Size.x].Transparent))
                && !Blocks[index].HasFace(Direction.South))
            {
                Blocks[index].SetFace(Direction.South, true);
                AddTriangles(Direction.South);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, x, Direction.South, Direction.East, 1,
                        ChunkController.Size.x);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, y, Direction.South, Direction.Up,
                            ChunkController.YIndexStep, ChunkController.Size.y);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.South, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.South, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if ((((x == 0)
                  && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out block)
                  && block.Transparent)
                 || ((x > 0) && Blocks[index - 1].Transparent))
                && !Blocks[index].HasFace(Direction.West))
            {
                Blocks[index].SetFace(Direction.West, true);
                AddTriangles(Direction.West);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, z, Direction.West, Direction.North,
                        ChunkController.Size.x, ChunkController.Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, y, Direction.West, Direction.Up,
                            ChunkController.YIndexStep, ChunkController.Size.y);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.West, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.West, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if ((((y == (ChunkController.Size.y - 1))
                  && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.up, out block)
                  && block.Transparent)
                 || ((y < (ChunkController.Size.y - 1)) && Blocks[index + ChunkController.YIndexStep].Transparent))
                && !Blocks[index].HasFace(Direction.Up))
            {
                Blocks[index].SetFace(Direction.Up, true);
                AddTriangles(Direction.Up);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, z, Direction.Up, Direction.North,
                        ChunkController.Size.x, ChunkController.Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, x, Direction.Up, Direction.East, 1,
                            ChunkController.Size.x);

                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Up, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Up, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if ((y > 0)
                && Blocks[index - ChunkController.YIndexStep].Transparent
                && !Blocks[index].HasFace(Direction.Down))
            {
                Blocks[index].SetFace(Direction.Down, true);
                AddTriangles(Direction.Down);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, z, Direction.Down, Direction.North,
                        ChunkController.Size.x, ChunkController.Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, x, Direction.Down, Direction.East, 1,
                            ChunkController.Size.x);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Down, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Down, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }
        }

        private void AddTriangles(Direction direction)
        {
            int[] triangles = BlockFaces.Triangles.FaceTriangles[direction];

            foreach (int triangleValue in triangles)
            {
                _Triangles.Add(_Vertices.Count + triangleValue);
            }
        }

        private void AddVertices(Direction direction, Vector3 localPosition)
        {
            Vector3[] vertices = BlockFaces.Vertices.FaceVertices[direction];

            foreach (Vector3 vertex in vertices)
            {
                _Vertices.Add(vertex + localPosition);
            }
        }

        /// <summary>
        ///     Gets the total amount of possible traversals for face merging in a direction
        /// </summary>
        /// <param name="index">1D index of current block.</param>
        /// <param name="globalPosition">Global position of starting block.</param>
        /// <param name="slice">Current slice (x, y, or z) of a 3D index relative to your traversal direction.</param>
        /// <param name="traversalDirection">Direction to traverse in.</param>
        /// <param name="faceDirection">Direction to check faces while traversing.</param>
        /// <param name="traversalFactor">Amount of indexes to move forwards for each successful traversal in given direction.</param>
        /// <param name="limitingSliceValue">Maximum amount of traversals in given traversal direction.</param>
        /// <returns><see cref="int" /> representing how many successful traversals were made in the given direction.</returns>
        private int GetTraversals(
            int index, Vector3 globalPosition, int slice, Direction faceDirection,
            Direction traversalDirection, int traversalFactor, int limitingSliceValue)
        {
            // 1 being the current block at `index`
            int traversals = 1;

            // todo make aggressive face merging compatible with special block shapes
            if (!AggressiveFaceMerging)
            {
                return traversals;
            }

            // incrementing on x, so the traversal factor is 1
            // if we were incrementing on z, the factor would be Chunk.Size.x
            // and on y it would be (Chunk.YIndexStep)
            int traversalIndex = index + (traversals * traversalFactor);

            while ( // Set traversalIndex and ensure it is within the chunk's context
                ((slice + traversals) < limitingSliceValue)
                // This check removes the need to check if the adjacent block is transparent,
                // as our current block will never be transparent
                && (Blocks[index].Id == Blocks[traversalIndex].Id)
                && !Blocks[traversalIndex].HasFace(faceDirection)
                // ensure the block to the north of our current block is transparent
                && WorldController.Current.TryGetBlockAt(
                    globalPosition + (traversals * traversalDirection.AsVector3()) + faceDirection.AsVector3(),
                    out Block block)
                && block.Transparent)
            {
                Blocks[traversalIndex].SetFace(faceDirection, true);

                // increment and set traversal values
                traversals++;
                traversalIndex = index + (traversals * traversalFactor);
            }

            return traversals;
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
            int[] sizes = ChunkController.Size.ToArray();

            for (int axis = 0; axis < 3; axis++)
            {
                int i, j, k, l;
                int width, height;
                int axisIncrementedOne = (axis + 1) % 3;
                int axisIncrementedTwo = (axis + 2) % 3;
                int[] x = new int[3];
                int[] q = new int[3];

                // initialise largest possible mask, which is y * y
                bool[] mask = new bool[ChunkController.Size.y * ChunkController.Size.y];

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
                                    ? Blocks[blockAPosition.To1D(ChunkController.Size)].Id
                                    : BlockController.BLOCK_EMPTY_ID;

                            ushort blockBId = x[axis] < (sizes[axis] - 1)
                                ? Blocks[blockBPosition.To1D(ChunkController.Size)].Id
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
