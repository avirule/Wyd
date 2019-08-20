#region

using Controllers.Game;
using Controllers.World;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Environment.Terrain.Generation
{
    public struct ChunkGeneratorJob : IJobParallelFor
    {
        [ReadOnly]
        public Vector3Int Position;

        [ReadOnly]
        public NativeArray<Block> Blocks;

        [NativeDisableParallelForRestriction]
        public NativeList<Vector3> Vertices;

        [NativeDisableParallelForRestriction]
        public NativeList<Vector2> UVs;

        [NativeDisableParallelForRestriction]
        public NativeList<int> Triangles;

        public void Execute(int index)
        {
            int z = index / (Chunk.Size.y * Chunk.Size.x);
            int y = (index / Chunk.Size.x) % Chunk.Size.y;
            int x = index % Chunk.Size.x;

            if (Blocks[index].Id == BlockController.BLOCK_EMPTY_ID)
            {
                return;
            }

            Vector3Int globalPosition = Position + new Vector3Int(x, y, z);

            if (((z == (Chunk.Size.z - 1)) && WorldController.Current
                     .GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, 1)).Transparent) ||
                ((z < (Chunk.Size.z - 1)) && Blocks[index + (Chunk.Size.x * Chunk.Size.y)].Transparent))
            {
                Blocks[index].SetFace(Direction.North, true);

                Triangles.Add(Vertices.Length + 0);
                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 1);

                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 3);
                Triangles.Add(Vertices.Length + 1);

                Vertices.Add(new Vector3(x, y, z + 1));
                Vertices.Add(new Vector3(x, y + 1, z + 1));
                Vertices.Add(new Vector3(x + 1, y, z + 1));
                Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.North,
                    out Vector2[] uvs))
                {
                    UVs.Add(uvs[1]);
                    UVs.Add(uvs[3]);
                    UVs.Add(uvs[0]);
                    UVs.Add(uvs[2]);
                }
            }

            if (((x == (Chunk.Size.x - 1)) &&
                 WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.right)
                     .Transparent) ||
                ((x < (Chunk.Size.x - 1)) && Blocks[index + 1].Transparent))
            {
                Blocks[index].SetFace(Direction.East, true);

                Triangles.Add(Vertices.Length + 0);
                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 1);

                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 3);
                Triangles.Add(Vertices.Length + 1);

                Vertices.Add(new Vector3(x + 1, y, z));
                Vertices.Add(new Vector3(x + 1, y, z + 1));
                Vertices.Add(new Vector3(x + 1, y + 1, z));
                Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.East,
                    out Vector2[] uvs))
                {
                    UVs.Add(uvs[0]);
                    UVs.Add(uvs[1]);
                    UVs.Add(uvs[2]);
                    UVs.Add(uvs[3]);
                }
            }

            if (((z == 0) && WorldController.Current
                     .GetBlockAtPosition(globalPosition + new Vector3Int(0, 0, -1))
                     .Transparent) ||
                ((z > 0) && Blocks[index - (Chunk.Size.x * Chunk.Size.y)].Transparent))
            {
                Blocks[index].SetFace(Direction.South, true);

                Triangles.Add(Vertices.Length + 0);
                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 1);

                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 3);
                Triangles.Add(Vertices.Length + 1);

                Vertices.Add(new Vector3(x, y, z));
                Vertices.Add(new Vector3(x + 1, y, z));
                Vertices.Add(new Vector3(x, y + 1, z));
                Vertices.Add(new Vector3(x + 1, y + 1, z));

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.South,
                    out Vector2[] uvs))
                {
                    UVs.Add(uvs[0]);
                    UVs.Add(uvs[1]);
                    UVs.Add(uvs[2]);
                    UVs.Add(uvs[3]);
                }
            }

            if (((x == 0) && WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.left)
                     .Transparent) ||
                ((x > 0) && Blocks[index - 1].Transparent))
            {
                Blocks[index].SetFace(Direction.West, true);

                Triangles.Add(Vertices.Length + 0);
                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 1);

                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 3);
                Triangles.Add(Vertices.Length + 1);

                Vertices.Add(new Vector3(x, y, z));
                Vertices.Add(new Vector3(x, y + 1, z));
                Vertices.Add(new Vector3(x, y, z + 1));
                Vertices.Add(new Vector3(x, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.West,
                    out Vector2[] uvs))
                {
                    UVs.Add(uvs[1]);
                    UVs.Add(uvs[3]);
                    UVs.Add(uvs[0]);
                    UVs.Add(uvs[2]);
                }
            }

            if (((y == (Chunk.Size.y - 1)) &&
                 WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.up).Transparent) ||
                ((y < (Chunk.Size.y - 1)) && Blocks[index + Chunk.Size.x].Transparent))
            {
                Blocks[index].SetFace(Direction.Up, true);

                Triangles.Add(Vertices.Length + 0);
                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 1);

                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 3);
                Triangles.Add(Vertices.Length + 1);

                Vertices.Add(new Vector3(x, y + 1, z));
                Vertices.Add(new Vector3(x + 1, y + 1, z));
                Vertices.Add(new Vector3(x, y + 1, z + 1));
                Vertices.Add(new Vector3(x + 1, y + 1, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Up,
                    out Vector2[] uvs))
                {
                    UVs.Add(uvs[0]);
                    UVs.Add(uvs[2]);
                    UVs.Add(uvs[1]);
                    UVs.Add(uvs[3]);
                }
            }

            if (((y == 0) && WorldController.Current.GetBlockAtPosition(globalPosition + Vector3Int.down)
                     .Transparent) ||
                ((y > 0) && Blocks[index - Chunk.Size.x].Transparent))
            {
                Blocks[index].SetFace(Direction.Down, true);

                Triangles.Add(Vertices.Length + 0);
                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 1);

                Triangles.Add(Vertices.Length + 2);
                Triangles.Add(Vertices.Length + 3);
                Triangles.Add(Vertices.Length + 1);

                Vertices.Add(new Vector3(x, y, z));
                Vertices.Add(new Vector3(x, y, z + 1));
                Vertices.Add(new Vector3(x + 1, y, z));
                Vertices.Add(new Vector3(x + 1, y, z + 1));

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Down,
                    out Vector2[] uvs))
                {
                    UVs.Add(uvs[0]);
                    UVs.Add(uvs[1]);
                    UVs.Add(uvs[2]);
                    UVs.Add(uvs[3]);
                }
            }
        }

        public Mesh GetMesh()
        {
            Mesh mesh = new Mesh();

            if ((Vertices.Length == 0) ||
                (Triangles.Length == 0))
            {
                return mesh;
            }

            if (Vertices.Length > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = Vertices.ToArray();
            mesh.uv = UVs.ToArray();
            mesh.triangles = Triangles.ToArray();

            Blocks.Dispose();
            Vertices.Dispose();
            UVs.Dispose();
            Triangles.Dispose();

            mesh.RecalculateTangents();
            mesh.RecalculateNormals();
            mesh.Optimize();

            return mesh;
        }
    }
}