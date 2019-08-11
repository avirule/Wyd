#region

using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using Environment;
using Environment.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Threading.Generation
{
    public class MeshGenerator : ThreadedProcess
    {
        private readonly BlockController _BlockController;
        private readonly Block[] _Blocks;
        private readonly Vector3Int _Position;

        private readonly List<int> _Triangles;
        private readonly List<Vector2> _UVs;
        private readonly List<Vector3> _Vertices;

        private readonly WorldController _WorldController;
        private bool _IsVisible;

        public MeshGenerator(WorldController worldController, BlockController blockController, Vector3Int position,
            Block[] blocks)
        {
            _WorldController = worldController;
            _BlockController = blockController;
            _Triangles = new List<int>();
            _UVs = new List<Vector2>();
            _Vertices = new List<Vector3>();
            _IsVisible = false;
            _Position = position;
            _Blocks = blocks;
        }

        protected override void ThreadFunction()
        {
            for (int x = 0; x < Chunk.Size.x; x++)
            {
                for (int y = 0; y < Chunk.Size.y; y++)
                {
                    for (int z = 0; z < Chunk.Size.z; z++)
                    {
                        if (FlagAbort)
                        {
                            return;
                        }
                        
                        int index = x + (Chunk.Size.x * (y + (Chunk.Size.y * z)));

                        if (_Blocks[index].Id == BlockController.BLOCK_EMPTY_ID)
                        {
                            continue;
                        }

                        Vector3Int globalPosition = _Position + new Vector3Int(x, y, z);

                        if (((z == (Chunk.Size.z - 1)) && !_WorldController
                                 .GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, 1)).Opaque) ||
                            ((z < (Chunk.Size.z - 1)) && !_Blocks[index + (Chunk.Size.x * Chunk.Size.y)].Opaque))
                        {
                            _Blocks[index].SetFace(Direction.North, true);

                            _Vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z + 1),
                                new Vector3(x, y + 1, z + 1),
                                new Vector3(x + 1, y, z + 1),
                                new Vector3(x + 1, y + 1, z + 1)
                            });

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition, Direction.North,
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

                            int vertexIndex = _Vertices.Count - 4;

                            _Triangles.AddRange(new[]
                            {
                                vertexIndex + 0,
                                vertexIndex + 2,
                                vertexIndex + 1,

                                vertexIndex + 2,
                                vertexIndex + 3,
                                vertexIndex + 1
                            });
                        }

                        if (((x == (Chunk.Size.x - 1)) &&
                             !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.right).Opaque) ||
                            ((x < (Chunk.Size.x - 1)) && !_Blocks[index + 1].Opaque))
                        {
                            _Blocks[index].SetFace(Direction.East, true);

                            _Vertices.AddRange(new[]
                            {
                                new Vector3(x + 1, y, z),
                                new Vector3(x + 1, y, z + 1),
                                new Vector3(x + 1, y + 1, z),
                                new Vector3(x + 1, y + 1, z + 1)
                            });

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition, Direction.East,
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

                            int vertexIndex = _Vertices.Count - 4;

                            _Triangles.AddRange(new[]
                            {
                                vertexIndex + 0,
                                vertexIndex + 2,
                                vertexIndex + 1,

                                vertexIndex + 2,
                                vertexIndex + 3,
                                vertexIndex + 1
                            });
                        }

                        if (((z == 0) && !_WorldController.GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, -1))
                                 .Opaque) ||
                            ((z > 0) && !_Blocks[index - (Chunk.Size.x * Chunk.Size.y)].Opaque))
                        {
                            _Blocks[index].SetFace(Direction.South, true);

                            _Vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z),
                                new Vector3(x + 1, y, z),
                                new Vector3(x, y + 1, z),
                                new Vector3(x + 1, y + 1, z)
                            });

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition, Direction.South,
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

                            int vertexIndex = _Vertices.Count - 4;

                            _Triangles.AddRange(new[]
                            {
                                vertexIndex + 0,
                                vertexIndex + 2,
                                vertexIndex + 1,

                                vertexIndex + 2,
                                vertexIndex + 3,
                                vertexIndex + 1
                            });
                        }

                        if (((x == 0) && !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.left)
                                 .Opaque) ||
                            ((x > 0) && !_Blocks[index - 1].Opaque))
                        {
                            _Blocks[index].SetFace(Direction.West, true);

                            _Vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z),
                                new Vector3(x, y + 1, z),
                                new Vector3(x, y, z + 1),
                                new Vector3(x, y + 1, z + 1)
                            });

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition, Direction.West,
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

                            int vertexIndex = _Vertices.Count - 4;

                            _Triangles.AddRange(new[]
                            {
                                vertexIndex + 0,
                                vertexIndex + 2,
                                vertexIndex + 1,

                                vertexIndex + 2,
                                vertexIndex + 3,
                                vertexIndex + 1
                            });
                        }

                        if (((y == (Chunk.Size.y - 1)) &&
                             !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.up).Opaque) ||
                            ((y < (Chunk.Size.y - 1)) && !_Blocks[index + Chunk.Size.x].Opaque))
                        {
                            _Blocks[index].SetFace(Direction.Up, true);

                            _Vertices.AddRange(new[]
                            {
                                new Vector3(x, y + 1, z),
                                new Vector3(x + 1, y + 1, z),
                                new Vector3(x, y + 1, z + 1),
                                new Vector3(x + 1, y + 1, z + 1)
                            });

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition, Direction.Up,
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

                            int vertexIndex = _Vertices.Count - 4;

                            _Triangles.AddRange(new[]
                            {
                                vertexIndex + 0,
                                vertexIndex + 2,
                                vertexIndex + 1,

                                vertexIndex + 2,
                                vertexIndex + 3,
                                vertexIndex + 1
                            });
                        }

                        if (((y == 0) && !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.down)
                                 .Opaque) ||
                            ((y > 0) && !_Blocks[index - Chunk.Size.x].Opaque))
                        {
                            _Blocks[index].SetFace(Direction.Down, true);

                            _Vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z),
                                new Vector3(x, y, z + 1),
                                new Vector3(x + 1, y, z),
                                new Vector3(x + 1, y, z + 1)
                            });

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition, Direction.Down,
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

                            int vertexIndex = _Vertices.Count - 4;

                            _Triangles.AddRange(new[]
                            {
                                vertexIndex + 0,
                                vertexIndex + 2,
                                vertexIndex + 1,

                                vertexIndex + 2,
                                vertexIndex + 3,
                                vertexIndex + 1
                            });
                        }

                        _IsVisible = true;
                    }
                }
            }
        }

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh();

            if ((_IsVisible == false) || (_Vertices.Count == 0))
            {
                return mesh;
            }

            if (_Vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = _Vertices.ToArray();
            mesh.uv = _UVs.ToArray();
            mesh.triangles = _Triangles.ToArray();

            mesh.RecalculateTangents();
            mesh.RecalculateNormals();
            mesh.Optimize();

            return mesh;
        }
    }
}