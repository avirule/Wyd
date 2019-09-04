#region

using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using Game;
using Game.World.Block;
using Game.World.Chunk;
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
        private ushort[] _Blocks;
        private bool _Greedy;

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
        public void Set(Vector3 position, ushort[] blocks, bool greedy)
        {
            _Vertices.Clear();
            _Triangles.Clear();
            _UVs.Clear();

            _Position = position;
            _Blocks = blocks;
            _Greedy = greedy;
        }

        protected override void Process()
        {
            if (_Blocks == default)
            {
                return;
            }

            if (_Greedy)
            {
                GenerateGreedyMeshData();
            }
            else
            {
                GenerateNaiveMeshData();
            }
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

        private void GenerateNaiveMeshData()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
                if (AbortToken.IsCancellationRequested)
                {
                    return;
                }

                if (_Blocks[index] == BlockController.BLOCK_EMPTY_ID)
                {
                    continue;
                }

                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);
                Vector3 localPosition = new Vector3(x, y, z);
                Vector3 globalPosition = _Position + new Vector3(x, y, z);
                Vector3 uvSize = new Vector3(1f, 0, 1f);

                if (((z == (Chunk.Size.z - 1)) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.forward))) ||
                    ((z < (Chunk.Size.z - 1)) &&
                     BlockController.Current.IsBlockTransparent(_Blocks[index + Chunk.Size.x])))
                {
                    AddTriangles(Direction.North);
                    AddVertices(Direction.North, localPosition);

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.North, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                    }
                }

                if (((x == (Chunk.Size.x - 1)) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.right))) ||
                    ((x < (Chunk.Size.x - 1)) && BlockController.Current.IsBlockTransparent(_Blocks[index + 1])))
                {
                    AddTriangles(Direction.East);
                    AddVertices(Direction.East, localPosition);

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.East, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if (((z == 0) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.back))) ||
                    ((z > 0) && BlockController.Current.IsBlockTransparent(_Blocks[index - Chunk.Size.x])))
                {
                    AddTriangles(Direction.South);
                    AddVertices(Direction.South, localPosition);

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.South, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if (((x == 0) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.left))) ||
                    ((x > 0) && BlockController.Current.IsBlockTransparent(_Blocks[index - 1])))
                {
                    AddTriangles(Direction.West);
                    AddVertices(Direction.West, localPosition);

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.West, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                    }
                }

                if (((y == (Chunk.Size.y - 1)) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.up))) ||
                    ((y < (Chunk.Size.y - 1)) &&
                     BlockController.Current.IsBlockTransparent(_Blocks[index + (Chunk.Size.x * Chunk.Size.z)])))
                {
                    AddTriangles(Direction.Up);
                    AddVertices(Direction.Up, localPosition);

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.Up, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if ((y > 0) && BlockController.Current.IsBlockTransparent(
                        _Blocks[index - (Chunk.Size.x * Chunk.Size.z)]))
                {
                    AddTriangles(Direction.Down);
                    AddVertices(Direction.Down, localPosition);

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.Down, uvSize, out Vector3[] uvs))
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

        #endregion

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
                    for (x[axisIncrementedTwo] = 0; x[axisIncrementedTwo] < sizes[axisIncrementedTwo]; ++x[axisIncrementedTwo])
                    {
                        for (x[axisIncrementedOne] = 0; x[axisIncrementedOne] < sizes[axisIncrementedOne]; ++x[axisIncrementedOne])
                        {
                            bool maskValue;

                            Vector3 blockAPosition = new Vector3(x[0], x[1], x[2]);
                            Vector3 blockBPosition = new Vector3(x[0] + q[0], x[1] + q[1], x[2] + q[2]);

                            ushort blockAId =
                                0 <= x[axis] ?
                                _Blocks[blockAPosition.To1D(Chunk.Size)] :
                                BlockController.BLOCK_EMPTY_ID;

                            ushort blockBId = x[axis] < (sizes[axis] - 1) ?
                                _Blocks[blockBPosition.To1D(Chunk.Size)] :
                                WorldController.Current.GetBlockAt(blockBPosition);

                            bool blockAIsTransparent =!BlockController.Current.IsBlockTransparent(blockAId);
                            bool blockBIsTransparent =!BlockController.Current.IsBlockTransparent(blockBId);
                            
                            // decide if a face should be drawn based on whether two neighbors are opposite transparency
                            // and also if the block types match
                            maskValue = blockAIsTransparent != blockBIsTransparent && blockAId != blockBId;
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
            _Triangles.Add(_Vertices.Count + WorldController.Current.TriValues[0]);
            _Triangles.Add(_Vertices.Count + WorldController.Current.TriValues[1]);
            _Triangles.Add(_Vertices.Count + WorldController.Current.TriValues[2]);
            _Triangles.Add(_Vertices.Count + WorldController.Current.TriValues[3]);
            _Triangles.Add(_Vertices.Count + WorldController.Current.TriValues[4]);
            _Triangles.Add(_Vertices.Count + WorldController.Current.TriValues[5]);

            // 0   1
            //
            // 2   3
            
            _Vertices.Add(v1);
            _Vertices.Add(v2);
            _Vertices.Add(v3);
            _Vertices.Add(v4);
        }

        private static int Index3DTo1D(int x, int y, int z)
        {
            return x + (z * Chunk.Size.x) + (y * Chunk.Size.x * Chunk.Size.z);
        }
    }
}