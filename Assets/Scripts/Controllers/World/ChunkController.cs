using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Environment.Terrain;
using Static;
using UnityEngine;
using UnityEngine.Rendering;

namespace Controllers.World
{
    public sealed class ChunkController : MonoBehaviour
    {
        public Mesh AggregateMesh;

        public bool Building;
        public Dictionary<Vector3Int, Chunk> Chunks;
        public bool Meshed;
        public bool Meshing;
        public bool ChunksGenerated => Chunks.Values.All(chunk => chunk.Generated);
        public bool ChunksMeshed => Chunks.Values.All(chunk => chunk.Meshed);

        private void Awake()
        {
            Chunks = new Dictionary<Vector3Int, Chunk>();
            Building = false;
        }

        public void Tick(BoundsInt loadedBounds)
        {
            if (Meshing || !ChunksGenerated)
            {
                return;
            }

            AllocateDestroyableChunks(loadedBounds);
            GenerateMissingChunkMeshes();

            if (!ChunksMeshed || Meshing || Meshed || Building)
            {
                return;
            }

            StartCoroutine(GenerateCombinedMesh());
        }

        private IEnumerator GenerateCombinedMesh()
        {
            Meshing = true;
            Meshed = false;

            Stopwatch frameCounter = new Stopwatch();
            float totalElapsed = 0f;

            int index = 0;
            CombineInstance[] combines = new CombineInstance[Chunks.Count];

            foreach (Chunk chunk in Chunks.Values)
            {
                frameCounter.Restart();

                CombineInstance combine = new CombineInstance
                {
                    mesh = chunk.Mesh,
                    transform = Matrix4x4.TRS(chunk.Position, Quaternion.identity, new Vector3(1f, 1f, 1f))
                };

                combines[index] = combine;
                index++;


                frameCounter.Stop();
                totalElapsed += (float) frameCounter.Elapsed.TotalSeconds;

                if (totalElapsed >= WorldController.WORLD_TICK_RATE)
                {
                    yield return null;
                }
            }

            AggregateMesh = new Mesh {indexFormat = IndexFormat.UInt32};
            AggregateMesh.CombineMeshes(combines, true, true);
            AggregateMesh.RecalculateNormals();
            AggregateMesh.RecalculateTangents();
            AggregateMesh.Optimize();

            Meshing = false;
            Meshed = true;
        }

        public Block GetBlockAtPosition(Vector3Int position)
        {
            Vector3Int chunkPosition = WorldController.GetWorldChunkOriginFromGlobalPosition(position).ToInt();

            Chunks.TryGetValue(chunkPosition, out Chunk chunk);

            if (chunk == null)
            {
                return default;
            }

            Vector3Int localPosition = (position - chunkPosition).Abs();

            if ((chunk.Blocks.Length <= localPosition.x) ||
                (chunk.Blocks[0].Length <= localPosition.y) ||
                (chunk.Blocks[0][0].Length <= localPosition.z))
            {
                return default;
            }

            Block block = chunk.Blocks[localPosition.x][localPosition.y][localPosition.z];

            return block;
        }


        #region CHUNK DESTROYING

        private void AllocateDestroyableChunks(BoundsInt bounds)
        {
            foreach (Vector3Int position in Chunks.Keys.Where(position =>
                !Mathv.ContainsVector3Int(bounds, position)).ToList())
            {
                DestroyChunk(Chunks[position]);
            }
        }

        private void DestroyChunk(Chunk chunk)
        {
            chunk.PendingDestruction = true;
            Chunks.Remove(chunk.Position);

            Meshed = false;
        }

        private void GenerateMissingChunkMeshes()
        {
            foreach (Chunk chunk in Chunks.Values)
            {
                if (chunk.PendingDestruction || !chunk.PendingUpdate || (!chunk.Meshed && chunk.Meshing))
                {
                    continue;
                }

                StartCoroutine(chunk.GenerateMesh());
            }
        }

        #endregion


        #region CHUNK BUILDING

        public IEnumerator BuildChunkArea(Vector3Int origin, int radius)
        {
            // +1 to include player's chunk
            for (int x = -radius; x < (radius + 1); x++)
            {
                for (int z = -radius; z < (radius + 1); z++)
                {
                    Vector3Int pos = new Vector3Int(x * Chunk.Size.x, 0, z * Chunk.Size.x) + origin;

                    if ((Chunks == null) || Chunks.ContainsKey(pos))
                    {
                        continue;
                    }

                    CreateChunk(pos);

                    yield return null;
                }
            }
        }

        private void CreateChunk(Vector3Int position)
        {
            Chunk chunk = new Chunk(position);

            Chunks.Add(chunk.Position, chunk);

            StartCoroutine(chunk.GenerateBlocks());
        }

        #endregion
    }
}