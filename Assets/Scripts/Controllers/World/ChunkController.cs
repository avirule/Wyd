#region

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Environment.Terrain;
using Static;
using UnityEngine;

#endregion

namespace Controllers.World
{
    public sealed class ChunkController : MonoBehaviour
    {
        private Stopwatch _BuildChunkQueueStopwatch;
        private Chunk _ChunkObject;
        private List<Chunk> _DestroyedChunks;

        public int MaximumChunkCache;
        public int MaximumChunkLoadTimeCaching;
        public bool BuildingChunkArea;
        public List<Chunk> Chunks;
        public Queue<Vector3Int> BuildChunkQueue;
        public bool AllChunksGenerated => Chunks.All(chunk => chunk.Generated);
        public bool AllChunksMeshed => Chunks.All(chunk => chunk.Meshed);

        private void Awake()
        {
            _ChunkObject = Resources.Load<Chunk>(@"Environment\Terrain\Chunk");
            _DestroyedChunks = new List<Chunk>();
            _BuildChunkQueueStopwatch = new Stopwatch();
            Chunks = new List<Chunk>();
            BuildChunkQueue = new Queue<Vector3Int>();
            BuildingChunkArea = false;
        }

        public void Tick(BoundsInt bounds)
        {
            if (BuildChunkQueue.Count > 0)
            {
                StartCoroutine(ProcessBuildChunkQueue());
            }

            if (AllChunksGenerated)
            {
                DestroyOutOfBoundsChunks(bounds);
                BeginGeneratingMissingChunkMeshes();
            }

            if (_DestroyedChunks.Count > MaximumChunkCache)
            {
                _DestroyedChunks.RemoveRange(0, _DestroyedChunks.Count - MaximumChunkCache);
            }
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
            AssignNeighborsPendingUpdate(chunk.Position);
            chunk.Destroy();
            Chunks.Remove(chunk);
        }

        private void BeginGeneratingMissingChunkMeshes()
        {
            foreach (Chunk chunk in Chunks)
            {
                if (chunk.Destroyed || !chunk.PendingMeshUpdate || (!chunk.Meshed && chunk.Meshing))
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
            BuildingChunkArea = true;

            float totalElapsed = 0f;

            while (BuildChunkQueue.Count > 0)
            {
                _BuildChunkQueueStopwatch.Restart();

                Vector3Int pos = BuildChunkQueue.Dequeue();

                if (!CheckChunkExistsAtPosition(pos) || GetChunkAtPosition(pos).Destroyed)
                {
                    CreateChunk(pos);
                }

                _BuildChunkQueueStopwatch.Stop();
                totalElapsed += (float) _BuildChunkQueueStopwatch.Elapsed.TotalSeconds;

                if (totalElapsed >= WorldController.WORLD_TICK_RATE)
                {
                    yield return null;
                }
            }

            BuildingChunkArea = false;
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

            // ensures that neighbours update their meshes to cull newly out of sight faces
            AssignNeighborsPendingUpdate(chunk.Position);
        }

        private void AssignNeighborsPendingUpdate(Vector3Int position)
        {
            for (int x = -1; x < 2; x++)
            {
                Chunk chunkAtPosition = GetChunkAtPosition(position + new Vector3Int(x * Chunk.Size.x, 0, 0));

                if (chunkAtPosition == default)
                {
                    continue;
                }

                chunkAtPosition.PendingMeshUpdate = true;
            }

            for (int z = -2; z < 2; z++)
            {
                Chunk chunkAtPosition = GetChunkAtPosition(position + new Vector3Int(0, 0, z * Chunk.Size.z));

                if (chunkAtPosition == default)
                {
                    continue;
                }

                chunkAtPosition.PendingMeshUpdate = true;
            }
        }

        #endregion


        #region MISC

        public bool CheckChunkExistsAtPosition(Vector3Int position)
        {
            return Chunks.Any(chunk => chunk.Position == position);
        }

        public Chunk GetChunkAtPosition(Vector3Int position)
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
            int localPosition1d =
                localPosition.x + (Chunk.Size.x * (localPosition.y + (Chunk.Size.y * localPosition.z)));

            if (chunk.Blocks.Length <= localPosition1d)
            {
                return default;
            }

            Block block = chunk.Blocks[localPosition1d];

            return block;
        }

        #endregion
    }
}