#region

using Controllers.Game;
using Controllers.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Environment.Terrain.Generation
{
    [BurstCompile]
    public struct ChunkGeneratorJob : IJobParallelFor
    {
        private NativeArray<Block> _Blocks;
        private readonly Vector3Int _Position;
        [DeallocateOnJobCompletion]
        private NativeList<Vector3> _Vertices;
        [DeallocateOnJobCompletion]
        private NativeList<Vector2> _UVs;
        [DeallocateOnJobCompletion]
        private NativeList<int> _Triangles;
        private bool _IsVisible;

        public ChunkGeneratorJob(Vector3Int position, Block[] blocks)
        {
            _Triangles = new NativeList<int>(Allocator.Persistent);
            _UVs = new NativeList<Vector2>(Allocator.Persistent);
            _Vertices = new NativeList<Vector3>(Allocator.Persistent);
            _IsVisible = false;
            _Position = position;
            _Blocks = new NativeArray<Block>(blocks, Allocator.Persistent);
        }

        public void Execute(int index)
        {
            int x = index / (Chunk.Size.y * Chunk.Size.z);
            int y = (index / Chunk.Size.z) * Chunk.Size.y;
            int z = index % Chunk.Size.z;

            if (_Blocks[index].Id == BlockController.BLOCK_EMPTY_ID)
            {
                return;
            }

            Vector3Int globalPosition = _Position + new Vector3Int(x, y, z);

            if (((z == (Chunk.Size.z - 1)) && WorldController.Current
                     .GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, 1)).Transparent) ||
                ((z < (Chunk.Size.z - 1)) && _Blocks[index + (Chunk.Size.x * Chunk.Size.y)].Transparent))
            {
                _Blocks[index].SetFace(Direction.North, true);

                _Triangles.Add(_Vertices.Length + 0);
                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 1);

                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 3);
                _Triangles.Add(_Vertices.Length + 1);

                _Vertices.Add(new Vector3(x, y, z + 1));
                _Vertices.Add(new Vector3(x, y + 1, z + 1));
                _Vertices.Add(new Vector3(x + 1, y, z + 1));
                _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.North,
                    out Vector2[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (((x == (Chunk.Size.x - 1)) &&
                 WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.right)
                     .Transparent) ||
                ((x < (Chunk.Size.x - 1)) && _Blocks[index + 1].Transparent))
            {
                _Blocks[index].SetFace(Direction.East, true);

                _Triangles.Add(_Vertices.Length + 0);
                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 1);

                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 3);
                _Triangles.Add(_Vertices.Length + 1);

                _Vertices.Add(new Vector3(x + 1, y, z));
                _Vertices.Add(new Vector3(x + 1, y, z + 1));
                _Vertices.Add(new Vector3(x + 1, y + 1, z));
                _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.East,
                    out Vector2[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (((z == 0) && WorldController.Current
                     .GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, -1))
                     .Transparent) ||
                ((z > 0) && _Blocks[index - (Chunk.Size.x * Chunk.Size.y)].Transparent))
            {
                _Blocks[index].SetFace(Direction.South, true);

                _Triangles.Add(_Vertices.Length + 0);
                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 1);

                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 3);
                _Triangles.Add(_Vertices.Length + 1);

                _Vertices.Add(new Vector3(x, y, z));
                _Vertices.Add(new Vector3(x + 1, y, z));
                _Vertices.Add(new Vector3(x, y + 1, z));
                _Vertices.Add(new Vector3(x + 1, y + 1, z));

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.South,
                    out Vector2[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (((x == 0) && WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.left)
                     .Transparent) ||
                ((x > 0) && _Blocks[index - 1].Transparent))
            {
                _Blocks[index].SetFace(Direction.West, true);

                _Triangles.Add(_Vertices.Length + 0);
                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 1);

                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 3);
                _Triangles.Add(_Vertices.Length + 1);

                _Vertices.Add(new Vector3(x, y, z));
                _Vertices.Add(new Vector3(x, y + 1, z));
                _Vertices.Add(new Vector3(x, y, z + 1));
                _Vertices.Add(new Vector3(x, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.West,
                    out Vector2[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (((y == (Chunk.Size.y - 1)) &&
                 WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.up).Transparent) ||
                ((y < (Chunk.Size.y - 1)) && _Blocks[index + Chunk.Size.x].Transparent))
            {
                _Blocks[index].SetFace(Direction.Up, true);

                _Triangles.Add(_Vertices.Length + 0);
                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 1);

                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 3);
                _Triangles.Add(_Vertices.Length + 1);

                _Vertices.Add(new Vector3(x, y + 1, z));
                _Vertices.Add(new Vector3(x + 1, y + 1, z));
                _Vertices.Add(new Vector3(x, y + 1, z + 1));
                _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.Up,
                    out Vector2[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (((y == 0) && WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.down)
                     .Transparent) ||
                ((y > 0) && _Blocks[index - Chunk.Size.x].Transparent))
            {
                _Blocks[index].SetFace(Direction.Down, true);

                _Triangles.Add(_Vertices.Length + 0);
                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 1);

                _Triangles.Add(_Vertices.Length + 2);
                _Triangles.Add(_Vertices.Length + 3);
                _Triangles.Add(_Vertices.Length + 1);

                _Vertices.Add(new Vector3(x, y, z));
                _Vertices.Add(new Vector3(x, y, z + 1));
                _Vertices.Add(new Vector3(x + 1, y, z));
                _Vertices.Add(new Vector3(x + 1, y, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.Down,
                    out Vector2[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            _IsVisible = true;

            _Blocks.Dispose();
        }

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh();

            if ((_IsVisible == false) || (_Vertices.Length == 0))
            {
                return mesh;
            }

            if (_Vertices.Length > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = _Vertices.ToArray();
            mesh.uv = _UVs.ToArray();
            mesh.triangles = _Triangles.ToArray();

            _Vertices.Dispose();
            _UVs.Dispose();
            _Triangles.Dispose();

            mesh.RecalculateTangents();
            mesh.RecalculateNormals();
            mesh.Optimize();

            return mesh;
        }
    }
}