using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Environment.Terrain;
using Static;
using UnityEngine;

namespace Controllers.World
{
    public sealed class ChunkController : MonoBehaviour
    {
        private Chunk _ChunkObject;
        private List<Chunk> _DestroyedChunks;

        public Queue<Vector3Int> BuildChunkQueue;

        public bool Building;
        public List<Chunk> Chunks;
        public bool ChunksGenerated => Chunks.All(chunk => chunk.Generated);
        public bool ChunksMeshed => Chunks.All(chunk => chunk.Meshed);

        private void Awake()
        {
            _ChunkObject = Resources.Load<Chunk>(@"Environment\Terrain\Chunk");
            _DestroyedChunks = new List<Chunk>();
            Chunks = new List<Chunk>();
            BuildChunkQueue = new Queue<Vector3Int>();
            Building = false;
        }

        public void Tick(BoundsInt bounds)
        {
            if (Building)
            {
                return;
            }

            if (BuildChunkQueue.Count > 0)
            {
                StartCoroutine(ProcessBuildChunkQueue());
            }

            if (!ChunksGenerated)
            {
                return;
            }

            DestroyOutOfBoundsChunks(bounds);
            GenerateMissingChunkMeshes();

//            if (!ChunksMeshed || Meshing || Meshed || Building)
//            {
//                return;
//            }
//
//            StartCoroutine(GenerateCombinedMesh());
        }

        public Chunk GetChunkAtPosition(Vector3 position)
        {
            return Chunks.FirstOrDefault(chunk => chunk.Position == position);
        }

        public Block GetBlockAtPosition(Vector3Int position)
        {
            Vector3Int chunkPosition = WorldController.GetWorldChunkOriginFromGlobalPosition(position).ToInt();

            Chunk chunk = GetChunkAtPosition(chunkPosition);

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

        private void DestroyOutOfBoundsChunks(BoundsInt bounds)
        {
            int initialCount = Chunks.Count;

            for (int i = initialCount - 1; i >= 0; i--)
            {
                if (Mathv.ContainsVector3Int(bounds, Chunks[i].Position))
                {
                    continue;
                }

                _DestroyedChunks.Add(Chunks[i]);
                DestroyChunk(Chunks[i]);
            }
        }

        private void DestroyChunk(Chunk chunk)
        {
            chunk.Destroy();
            Chunks.Remove(chunk);
        }

        private void GenerateMissingChunkMeshes()
        {
            foreach (Chunk chunk in Chunks)
            {
                if (chunk.Destroyed || !chunk.PendingUpdate || (!chunk.Meshed && chunk.Meshing))
                {
                    continue;
                }

                StartCoroutine(chunk.GenerateMesh());
            }
        }

        #endregion


        #region CHUNK BUILDING

        public IEnumerator ProcessBuildChunkQueue()
        {
            Building = true;

            Stopwatch frameCounter = new Stopwatch();
            float totalElapsed = 0f;

            while (BuildChunkQueue.Count > 0)
            {
                frameCounter.Restart();

                Vector3Int pos = BuildChunkQueue.Dequeue();

                CreateChunk(pos);

                frameCounter.Stop();
                totalElapsed += (float) frameCounter.Elapsed.TotalSeconds;

                if (totalElapsed >= WorldController.WORLD_TICK_RATE)
                {
                    yield return null;
                }
            }

            Building = false;
        }

        private void CreateChunk(Vector3Int position)
        {
            Chunk chunkAtPosition = GetChunkAtPosition(position);

            if ((chunkAtPosition != default) && !chunkAtPosition.Destroyed)
            {
                return;
            }

            Chunk chunk = _DestroyedChunks.FirstOrDefault() ?? Instantiate(_ChunkObject, position, Quaternion.identity);
            chunk.Initialise(position);

            _DestroyedChunks.Remove(chunk);
            Chunks.Add(chunk);

            StartCoroutine(chunk.GenerateBlocks());
        }

        #endregion
    }
}