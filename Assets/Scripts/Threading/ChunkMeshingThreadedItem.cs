#region

using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using Environment;
using Environment.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable InlineOutVariableDeclaration

#endregion

namespace Threading
{
    /// <summary>
    ///     ChunkGeneratorJob expects its NativeArrays to be sized to sufficiently sized
    /// </summary>
    public class ChunkMeshingThreadedItem : ThreadedItem
    {
        private readonly Vector3Int _Position;
        private readonly Block[] _Blocks;
        private readonly List<int> _Triangles;
        private readonly List<Vector3> _Vertices;
        private readonly List<Vector2> _UVs;

        public ChunkMeshingThreadedItem(Vector3Int position, Block[] blocks)
        {
            _Position = position;
            _Blocks = blocks;
            _Triangles = new List<int>();
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector2>();
        }

        protected override void Process()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
                int x = index % Chunk.Size.x;
                int y = (index / Chunk.Size.x) % Chunk.Size.y;
                int z = index / (Chunk.Size.x * Chunk.Size.y);

                if (_Blocks[index].Id == BlockController.BLOCK_EMPTY_ID)
                {
                    return;
                }

                Vector3Int globalPosition = _Position + new Vector3Int(x, y, z);
                Block block = default;

                if (((z == (Chunk.Size.z - 1)) &&
                     WorldController.Current.TryGetBlockAtPosition(globalPosition + new Vector3Int(0, 0, 1),
                         out block) && block.Transparent) ||
                    ((z < (Chunk.Size.z - 1)) && _Blocks[index + (Chunk.Size.x * Chunk.Size.y)].Transparent))
                {
                    _Blocks[index].SetFace(Direction.North, true);

                    _Triangles.AddRange(new[]
                    {
                        _Vertices.Count + 0,
                        _Vertices.Count + 2,
                        _Vertices.Count + 1,

                        _Vertices.Count + 2,
                        _Vertices.Count + 3,
                        _Vertices.Count + 1
                    });

                    _Vertices.AddRange(new[]
                    {
                        new Vector3(x, y, z + 1),
                        new Vector3(x, y + 1, z + 1),
                        new Vector3(x + 1, y, z + 1),
                        new Vector3(x + 1, y + 1, z + 1)
                    });

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.North,
                        out Vector2[] uvs))
                    {
                        _UVs.AddRange(new[]
                        {
                            uvs[1],
                            uvs[3],
                            uvs[0],
                            uvs[2]
                        });
                    }
                }

                if (((x == (Chunk.Size.x - 1)) &&
                     WorldController.Current.TryGetBlockAtPosition(globalPosition + Vector3Int.right, out block) &&
                     block.Transparent) ||
                    ((x < (Chunk.Size.x - 1)) && _Blocks[index + 1].Transparent))
                {
                    _Blocks[index].SetFace(Direction.East, true);

                    _Triangles.AddRange(new[]
                    {
                        _Vertices.Count + 0,
                        _Vertices.Count + 2,
                        _Vertices.Count + 1,

                        _Vertices.Count + 2,
                        _Vertices.Count + 3,
                        _Vertices.Count + 1
                    });

                    _Vertices.AddRange(new[]
                    {
                        new Vector3(x + 1, y, z),
                        new Vector3(x + 1, y, z + 1),
                        new Vector3(x + 1, y + 1, z),
                        new Vector3(x + 1, y + 1, z + 1)
                    });

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.East,
                        out Vector2[] uvs))
                    {
                        _UVs.AddRange(new[]
                        {
                            uvs[0],
                            uvs[1],
                            uvs[2],
                            uvs[3]
                        });
                    }
                }

                if (((z == 0) &&
                     WorldController.Current.TryGetBlockAtPosition(globalPosition + new Vector3Int(0, 0, -1),
                         out block) && block.Transparent) ||
                    ((z > 0) && _Blocks[index - (Chunk.Size.x * Chunk.Size.y)].Transparent))
                {
                    _Blocks[index].SetFace(Direction.South, true);

                    _Triangles.AddRange(new[]
                    {
                        _Vertices.Count + 0,
                        _Vertices.Count + 2,
                        _Vertices.Count + 1,

                        _Vertices.Count + 2,
                        _Vertices.Count + 3,
                        _Vertices.Count + 1
                    });

                    _Vertices.AddRange(new[]
                    {
                        new Vector3(x, y, z),
                        new Vector3(x + 1, y, z),
                        new Vector3(x, y + 1, z),
                        new Vector3(x + 1, y + 1, z)
                    });

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.South,
                        out Vector2[] uvs))
                    {
                        _UVs.AddRange(new[]
                        {
                            uvs[0],
                            uvs[1],
                            uvs[2],
                            uvs[3]
                        });
                    }
                }

                if (((x == 0) && WorldController.Current.TryGetBlockAtPosition(globalPosition + Vector3Int.left,
                         out block) && block.Transparent) ||
                    ((x > 0) && _Blocks[index - 1].Transparent))
                {
                    _Blocks[index].SetFace(Direction.West, true);

                    _Triangles.AddRange(new[]
                    {
                        _Vertices.Count + 0,
                        _Vertices.Count + 2,
                        _Vertices.Count + 1,

                        _Vertices.Count + 2,
                        _Vertices.Count + 3,
                        _Vertices.Count + 1
                    });

                    _Vertices.AddRange(new[]
                    {
                        new Vector3(x, y, z),
                        new Vector3(x, y + 1, z),
                        new Vector3(x, y, z + 1),
                        new Vector3(x, y + 1, z + 1)
                    });

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.West,
                        out Vector2[] uvs))
                    {
                        _UVs.AddRange(new[]
                        {
                            uvs[1],
                            uvs[3],
                            uvs[0],
                            uvs[2]
                        });
                    }
                }

                if (((y == (Chunk.Size.y - 1)) &&
                     WorldController.Current.TryGetBlockAtPosition(globalPosition + Vector3Int.up, out block) &&
                     block.Transparent) ||
                    ((y < (Chunk.Size.y - 1)) && _Blocks[index + Chunk.Size.x].Transparent))
                {
                    _Blocks[index].SetFace(Direction.Up, true);

                    _Triangles.AddRange(new[]
                    {
                        _Vertices.Count + 0,
                        _Vertices.Count + 2,
                        _Vertices.Count + 1,

                        _Vertices.Count + 2,
                        _Vertices.Count + 3,
                        _Vertices.Count + 1
                    });

                    _Vertices.AddRange(new[]
                    {
                        new Vector3(x, y + 1, z),
                        new Vector3(x + 1, y + 1, z),
                        new Vector3(x, y + 1, z + 1),
                        new Vector3(x + 1, y + 1, z + 1)
                    });

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.Up,
                        out Vector2[] uvs))
                    {
                        _UVs.AddRange(new[]
                        {
                            uvs[0],
                            uvs[2],
                            uvs[1],
                            uvs[3]
                        });
                    }
                }

                if (((y == 0) && WorldController.Current.TryGetBlockAtPosition(globalPosition + Vector3Int.down,
                         out block) && block.Transparent) ||
                    ((y > 0) && _Blocks[index - Chunk.Size.x].Transparent))
                {
                    _Blocks[index].SetFace(Direction.Down, true);

                    _Triangles.AddRange(new[]
                    {
                        _Vertices.Count + 0,
                        _Vertices.Count + 2,
                        _Vertices.Count + 1,

                        _Vertices.Count + 2,
                        _Vertices.Count + 3,
                        _Vertices.Count + 1
                    });

                    _Vertices.AddRange(new[]
                    {
                        new Vector3(x, y, z),
                        new Vector3(x, y, z + 1),
                        new Vector3(x + 1, y, z),
                        new Vector3(x + 1, y, z + 1)
                    });

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                        Direction.Down,
                        out Vector2[] uvs))
                    {
                        _UVs.AddRange(new[]
                        {
                            uvs[0],
                            uvs[1],
                            uvs[2],
                            uvs[3]
                        });
                    }
                }
            }
        }

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh();

            if ((_Vertices.Count == 0) ||
                (_Triangles.Count == 0))
            {
                return mesh;
            }

            if (_Vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = _Vertices.ToArray();
            mesh.triangles = _Triangles.ToArray();
            mesh.uv = _UVs.ToArray();

            mesh.RecalculateTangents();
            mesh.RecalculateNormals();
            mesh.Optimize();

            return mesh;
        }
    }
}