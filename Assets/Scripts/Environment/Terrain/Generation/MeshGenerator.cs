using System.Threading;
using Controllers;
using Controllers.Game;
using Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Environment.Terrain.Generation
{
    public class MeshGenerator : ThreadedProcess
    {
        private readonly BlockController _BlockController;
        private readonly Block[][][] _Blocks;
        private readonly Vector3Int _Position;

        private readonly WorldController _WorldController;
        private bool _IsVisible;

        private int _SizeInVectors;

        private int _TriangleIndex;
        private int[] _Triangles;
        private Vector2[] _UVs;
        private int _VertexIndex;
        private Vector3[] _Vertices;

        public MeshGenerator(WorldController worldController, BlockController blockController, Vector3Int position,
            Block[][][] blocks)
        {
            _WorldController = worldController;
            _BlockController = blockController;
            _SizeInVectors = _VertexIndex = _TriangleIndex = 0;
            _IsVisible = false;
            _Position = position;
            _Blocks = blocks;
        }

        public override void Start()
        {
            Thread = new Thread(Run);
            Thread.Start();
        }

        protected override void ThreadFunction()
        {
            for (int x = 0; x < Chunk.Size.x; x++)
            {
                for (int y = 0; y < Chunk.Size.y; y++)
                {
                    for (int z = 0; z < Chunk.Size.z; z++)
                    {
                        if (_Blocks[x][y][z].Id == BlockController.BLOCK_EMPTY_ID)
                        {
                            continue;
                        }

                        Vector3Int globalPosition = _Position + new Vector3Int(x, y, z);

                        if (((x == 0) && !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.left)
                                 .Opaque) ||
                            ((x > 0) && !_Blocks[x - 1][y][z].Opaque))
                        {
                            _Blocks[x][y][z].SetFace(Direction.West, true);
                            _SizeInVectors += 4;
                        }

                        if (((x == (Chunk.Size.x - 1)) &&
                             !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.right).Opaque) ||
                            ((x < (Chunk.Size.x - 1)) && !_Blocks[x + 1][y][z].Opaque))
                        {
                            _Blocks[x][y][z].SetFace(Direction.East, true);
                            _SizeInVectors += 4;
                        }


                        if (((y == 0) && !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.down)
                                 .Opaque) ||
                            ((y > 0) && !_Blocks[x][y - 1][z].Opaque))
                        {
                            _Blocks[x][y][z].SetFace(Direction.Down, true);
                            _SizeInVectors += 4;
                        }

                        if (((y == (Chunk.Size.y - 1)) &&
                             !_WorldController.GetBlockAtPosition(globalPosition + Vector3Int.up).Opaque) ||
                            ((y < (Chunk.Size.y - 1)) && !_Blocks[x][y + 1][z].Opaque))
                        {
                            _Blocks[x][y][z].SetFace(Direction.Up, true);
                            _SizeInVectors += 4;
                        }


                        if (((z == 0) && !_WorldController.GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, -1))
                                 .Opaque) ||
                            ((z > 0) && !_Blocks[x][y][z - 1].Opaque))
                        {
                            _Blocks[x][y][z].SetFace(Direction.South, true);
                            _SizeInVectors += 4;
                        }

                        if (((z == (Chunk.Size.x - 1)) && !_WorldController
                                 .GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, 1)).Opaque) ||
                            ((z < (Chunk.Size.x - 1)) && !_Blocks[x][y][z + 1].Opaque))
                        {
                            _Blocks[x][y][z].SetFace(Direction.North, true);
                            _SizeInVectors += 4;
                        }

                        _IsVisible = true;
                    }
                }
            }


            if (!_IsVisible)
            {
                return;
            }

            _Vertices = new Vector3[_SizeInVectors];
            _UVs = new Vector2[_SizeInVectors];
            _Triangles = new int[Mathf.CeilToInt(_SizeInVectors * 1.5f)];

            // Generate mesh
            for (int x = 0; x < Chunk.Size.x; x++)
            {
                for (int y = 0; y < Chunk.Size.y; y++)
                {
                    for (int z = 0; z < Chunk.Size.z; z++)
                    {
                        Vector3Int localPosition = new Vector3Int(x, y, z);
                        Vector3Int globalPosition = _Position + localPosition;

                        if (_Blocks[x][y][z].HasFace(Direction.North))
                        {
                            _Vertices[_VertexIndex + 0] =
                                new Vector3(localPosition.x, localPosition.y, localPosition.z + 1);
                            _Vertices[_VertexIndex + 1] =
                                new Vector3(localPosition.x, localPosition.y + 1, localPosition.z + 1);
                            _Vertices[_VertexIndex + 2] =
                                new Vector3(localPosition.x + 1, localPosition.y, localPosition.z + 1);
                            _Vertices[_VertexIndex + 3] =
                                new Vector3(localPosition.x + 1, localPosition.y + 1, localPosition.z + 1);

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[x][y][z].Id, globalPosition, Direction.North,
                                out Vector2[] uvs))
                            {
                                _UVs[_VertexIndex + 0] = uvs[1];
                                _UVs[_VertexIndex + 1] = uvs[3];
                                _UVs[_VertexIndex + 2] = uvs[0];
                                _UVs[_VertexIndex + 3] = uvs[2];
                            }

                            _Triangles[_TriangleIndex + 0] = _VertexIndex + 0;
                            _Triangles[_TriangleIndex + 1] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 2] = _VertexIndex + 1;

                            _Triangles[_TriangleIndex + 3] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 4] = _VertexIndex + 3;
                            _Triangles[_TriangleIndex + 5] = _VertexIndex + 1;

                            _VertexIndex += 4;
                            _TriangleIndex += 6;
                        }

                        if (_Blocks[x][y][z].HasFace(Direction.East))
                        {
                            _Vertices[_VertexIndex + 0] =
                                new Vector3(localPosition.x + 1, localPosition.y, localPosition.z);
                            _Vertices[_VertexIndex + 1] =
                                new Vector3(localPosition.x + 1, localPosition.y, localPosition.z + 1);
                            _Vertices[_VertexIndex + 2] =
                                new Vector3(localPosition.x + 1, localPosition.y + 1, localPosition.z);
                            _Vertices[_VertexIndex + 3] =
                                new Vector3(localPosition.x + 1, localPosition.y + 1, localPosition.z + 1);

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[x][y][z].Id, globalPosition, Direction.East,
                                out Vector2[] uvs))
                            {
                                _UVs[_VertexIndex + 0] = uvs[0];
                                _UVs[_VertexIndex + 1] = uvs[1];
                                _UVs[_VertexIndex + 2] = uvs[2];
                                _UVs[_VertexIndex + 3] = uvs[3];
                            }

                            _Triangles[_TriangleIndex + 0] = _VertexIndex + 0;
                            _Triangles[_TriangleIndex + 1] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 2] = _VertexIndex + 1;

                            _Triangles[_TriangleIndex + 3] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 4] = _VertexIndex + 3;
                            _Triangles[_TriangleIndex + 5] = _VertexIndex + 1;

                            _VertexIndex += 4;
                            _TriangleIndex += 6;
                        }

                        if (_Blocks[x][y][z].HasFace(Direction.South))
                        {
                            _Vertices[_VertexIndex + 0] =
                                new Vector3(localPosition.x, localPosition.y, localPosition.z);
                            _Vertices[_VertexIndex + 1] =
                                new Vector3(localPosition.x + 1, localPosition.y, localPosition.z);
                            _Vertices[_VertexIndex + 2] =
                                new Vector3(localPosition.x, localPosition.y + 1, localPosition.z);
                            _Vertices[_VertexIndex + 3] =
                                new Vector3(localPosition.x + 1, localPosition.y + 1, localPosition.z);

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[x][y][z].Id, globalPosition, Direction.South,
                                out Vector2[] uvs))
                            {
                                _UVs[_VertexIndex + 0] = uvs[0];
                                _UVs[_VertexIndex + 1] = uvs[1];
                                _UVs[_VertexIndex + 2] = uvs[2];
                                _UVs[_VertexIndex + 3] = uvs[3];
                            }

                            _Triangles[_TriangleIndex + 0] = _VertexIndex + 0;
                            _Triangles[_TriangleIndex + 1] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 2] = _VertexIndex + 1;

                            _Triangles[_TriangleIndex + 3] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 4] = _VertexIndex + 3;
                            _Triangles[_TriangleIndex + 5] = _VertexIndex + 1;

                            _VertexIndex += 4;
                            _TriangleIndex += 6;
                        }

                        if (_Blocks[x][y][z].HasFace(Direction.West))
                        {
                            _Vertices[_VertexIndex + 0] =
                                new Vector3(localPosition.x, localPosition.y, localPosition.z);
                            _Vertices[_VertexIndex + 1] =
                                new Vector3(localPosition.x, localPosition.y + 1, localPosition.z);
                            _Vertices[_VertexIndex + 2] =
                                new Vector3(localPosition.x, localPosition.y, localPosition.z + 1);
                            _Vertices[_VertexIndex + 3] =
                                new Vector3(localPosition.x, localPosition.y + 1, localPosition.z + 1);

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[x][y][z].Id, globalPosition, Direction.West,
                                out Vector2[] uvs))
                            {
                                _UVs[_VertexIndex + 0] = uvs[1];
                                _UVs[_VertexIndex + 1] = uvs[3];
                                _UVs[_VertexIndex + 2] = uvs[0];
                                _UVs[_VertexIndex + 3] = uvs[2];
                            }

                            _Triangles[_TriangleIndex + 0] = _VertexIndex + 0;
                            _Triangles[_TriangleIndex + 1] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 2] = _VertexIndex + 1;

                            _Triangles[_TriangleIndex + 3] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 4] = _VertexIndex + 3;
                            _Triangles[_TriangleIndex + 5] = _VertexIndex + 1;

                            _VertexIndex += 4;
                            _TriangleIndex += 6;
                        }

                        if (_Blocks[x][y][z].HasFace(Direction.Up))
                        {
                            _Vertices[_VertexIndex + 0] =
                                new Vector3(localPosition.x, localPosition.y + 1, localPosition.z);
                            _Vertices[_VertexIndex + 1] =
                                new Vector3(localPosition.x + 1, localPosition.y + 1, localPosition.z);
                            _Vertices[_VertexIndex + 2] =
                                new Vector3(localPosition.x, localPosition.y + 1, localPosition.z + 1);
                            _Vertices[_VertexIndex + 3] =
                                new Vector3(localPosition.x + 1, localPosition.y + 1, localPosition.z + 1);

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[x][y][z].Id, globalPosition, Direction.Up,
                                out Vector2[] uvs))
                            {
                                _UVs[_VertexIndex + 0] = uvs[0];
                                _UVs[_VertexIndex + 1] = uvs[2];
                                _UVs[_VertexIndex + 2] = uvs[1];
                                _UVs[_VertexIndex + 3] = uvs[3];
                            }

                            _Triangles[_TriangleIndex + 0] = _VertexIndex + 0;
                            _Triangles[_TriangleIndex + 1] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 2] = _VertexIndex + 1;

                            _Triangles[_TriangleIndex + 3] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 4] = _VertexIndex + 3;
                            _Triangles[_TriangleIndex + 5] = _VertexIndex + 1;

                            _VertexIndex += 4;
                            _TriangleIndex += 6;
                        }

                        if (_Blocks[x][y][z].HasFace(Direction.Down))
                        {
                            _Vertices[_VertexIndex + 0] =
                                new Vector3(localPosition.x, localPosition.y, localPosition.z);
                            _Vertices[_VertexIndex + 1] =
                                new Vector3(localPosition.x, localPosition.y, localPosition.z + 1);
                            _Vertices[_VertexIndex + 2] =
                                new Vector3(localPosition.x + 1, localPosition.y, localPosition.z);
                            _Vertices[_VertexIndex + 3] =
                                new Vector3(localPosition.x + 1, localPosition.y, localPosition.z + 1);

                            if (_BlockController.GetBlockSpriteUVs(_Blocks[x][y][z].Id, globalPosition, Direction.Down,
                                out Vector2[] uvs))
                            {
                                _UVs[_VertexIndex + 0] = uvs[0];
                                _UVs[_VertexIndex + 1] = uvs[1];
                                _UVs[_VertexIndex + 2] = uvs[2];
                                _UVs[_VertexIndex + 3] = uvs[3];
                            }

                            _Triangles[_TriangleIndex + 0] = _VertexIndex + 0;
                            _Triangles[_TriangleIndex + 1] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 2] = _VertexIndex + 1;

                            _Triangles[_TriangleIndex + 3] = _VertexIndex + 2;
                            _Triangles[_TriangleIndex + 4] = _VertexIndex + 3;
                            _Triangles[_TriangleIndex + 5] = _VertexIndex + 1;

                            _VertexIndex += 4;
                            _TriangleIndex += 6;
                        }
                    }
                }
            }
        }

        public Mesh GetMesh(ref Mesh copy)
        {
            if (copy == null)
            {
                copy = new Mesh();
            }
            else
            {
                copy.Clear();
            }

            if ((_IsVisible == false) || (_VertexIndex == 0))
            {
                return copy;
            }

            if (_VertexIndex > 65000)
            {
                copy.indexFormat = IndexFormat.UInt32;
            }

            copy.vertices = _Vertices;
            copy.uv = _UVs;
            copy.triangles = _Triangles;

            copy.RecalculateTangents();
            copy.RecalculateNormals();

            return copy;
        }
    }
}