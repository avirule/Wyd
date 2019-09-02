#region

using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using Game;
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
                // MeshGreedy();
            }
            else
            {
                MeshSimple();
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
            mesh.SetUVs(0, _UVs);

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        private void MeshSimple()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
                if (_Blocks[index] == BlockController.BLOCK_EMPTY_ID)
                {
                    continue;
                }

                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);
                Vector3 globalPosition = _Position + new Vector3(x, y, z);
                Vector3 uvSize = new Vector3(1f, 0, 1f);

                if (((z == (Chunk.Size.z - 1)) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.forward))) ||
                    ((z < (Chunk.Size.z - 1)) &&
                     BlockController.Current.IsBlockTransparent(_Blocks[index + Chunk.Size.x])))
                {
                    _Triangles.Add(_Vertices.Count + 0);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 1);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 3);
                    _Triangles.Add(_Vertices.Count + 1);

                    _Vertices.Add(new Vector3(x, y, z + 1));
                    _Vertices.Add(new Vector3(x, y + 1, z + 1));
                    _Vertices.Add(new Vector3(x + 1, y, z + 1));
                    _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

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
                    _Triangles.Add(_Vertices.Count + 0);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 1);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 3);
                    _Triangles.Add(_Vertices.Count + 1);

                    _Vertices.Add(new Vector3(x + 1, y, z));
                    _Vertices.Add(new Vector3(x + 1, y, z + 1));
                    _Vertices.Add(new Vector3(x + 1, y + 1, z));
                    _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

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
                    _Triangles.Add(_Vertices.Count + 0);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 1);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 3);
                    _Triangles.Add(_Vertices.Count + 1);

                    _Vertices.Add(new Vector3(x, y, z));
                    _Vertices.Add(new Vector3(x + 1, y, z));
                    _Vertices.Add(new Vector3(x, y + 1, z));
                    _Vertices.Add(new Vector3(x + 1, y + 1, z));

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
                    _Triangles.Add(_Vertices.Count + 0);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 1);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 3);
                    _Triangles.Add(_Vertices.Count + 1);

                    _Vertices.Add(new Vector3(x, y, z));
                    _Vertices.Add(new Vector3(x, y + 1, z));
                    _Vertices.Add(new Vector3(x, y, z + 1));
                    _Vertices.Add(new Vector3(x, y + 1, z + 1));

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
                    _Triangles.Add(_Vertices.Count + 0);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 1);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 3);
                    _Triangles.Add(_Vertices.Count + 1);

                    _Vertices.Add(new Vector3(x, y + 1, z));
                    _Vertices.Add(new Vector3(x + 1, y + 1, z));
                    _Vertices.Add(new Vector3(x, y + 1, z + 1));
                    _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.Up, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if (((y == 0) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.down))) ||
                    ((y > 0) && BlockController.Current.IsBlockTransparent(
                         _Blocks[index - (Chunk.Size.x * Chunk.Size.z)])))
                {
                    _Triangles.Add(_Vertices.Count + 0);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 1);
                    _Triangles.Add(_Vertices.Count + 2);
                    _Triangles.Add(_Vertices.Count + 3);
                    _Triangles.Add(_Vertices.Count + 1);

                    _Vertices.Add(new Vector3(x, y, z));
                    _Vertices.Add(new Vector3(x, y, z + 1));
                    _Vertices.Add(new Vector3(x + 1, y, z));
                    _Vertices.Add(new Vector3(x + 1, y, z + 1));

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
    }
}