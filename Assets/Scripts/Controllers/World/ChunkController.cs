#region

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Game;
using Environment.Terrain;
using Static;
using UnityEngine;

#endregion

namespace Controllers.World
{
    public sealed class ChunkController : MonoBehaviour
    {
        private Stopwatch _WorldTickLimiter;
        private Stopwatch _BuildChunkWorldTickLimiter;
        private Chunk _ChunkObject;
        private List<Chunk> _DeactivatedChunks;

        public bool BuildingChunkArea;
        public List<Chunk> Chunks;
        public Queue<Vector3Int> BuildChunkQueue;
        public bool AllChunksGenerated => Chunks.All(chunk => chunk.Generated);
        public bool AllChunksMeshed => Chunks.All(chunk => chunk.Meshed);

        private void Awake()
        {
            _ChunkObject = Resources.Load<Chunk>(@"Environment\Terrain\Chunk");
            _DeactivatedChunks = new List<Chunk>();
            _WorldTickLimiter = new Stopwatch();
            _BuildChunkWorldTickLimiter = new Stopwatch();
            Chunks = new List<Chunk>();
            BuildChunkQueue = new Queue<Vector3Int>();
            BuildingChunkArea = false;
        }

        public void Tick(BoundsInt bounds)
        {
            _WorldTickLimiter.Restart();
            
            if (BuildChunkQueue.Count > 0)
            {
                StartCoroutine(ProcessBuildChunkQueue());
            }

            if (AllChunksGenerated)
            {
                DestroyOutOfBoundsChunks(bounds);
                BeginGeneratingMissingChunkMeshes();
            }

            if (_DeactivatedChunks.Count > GameController.SettingsController.MaximumChunkCache)
            {
                CullDeactivatedChunks();
            }

            if (Chunks.Any(chunk => chunk.PendingMeshAssigment))
            {
                AssignMeshes();
            }

            _WorldTickLimiter.Stop();
        }

        private void AssignMeshes()
        {
            foreach (Chunk chunk in Chunks.Where(chunk => chunk.PendingMeshAssigment))
            {
                chunk.AssignMesh();

                if (_WorldTickLimiter.Elapsed.TotalSeconds >= WorldController.WORLD_TICK_RATE)
                {
                    break;
                }
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

                _DeactivatedChunks.Add(Chunks[i]);
                DestroyChunk(Chunks[i]);

                if (_WorldTickLimiter.Elapsed.TotalSeconds > WorldController.WORLD_TICK_RATE)
                {
                    break;
                }
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
                if (chunk.Deactivated || !chunk.PendingMeshUpdate || (!chunk.Meshed && chunk.Meshing))
                {
                    continue;
                }

                StartCoroutine(chunk.GenerateMesh());
                
                if (_WorldTickLimiter.Elapsed.TotalSeconds > WorldController.WORLD_TICK_RATE)
                {
                    break;
                }
            }
        }

        private void CullDeactivatedChunks()
        {
            while (_DeactivatedChunks.Count > GameController.SettingsController.MaximumChunkCache)
            {
                Chunk chunk = _DeactivatedChunks.First();
                Destroy(chunk);
                _DeactivatedChunks.Remove(chunk);

                if (_WorldTickLimiter.Elapsed.TotalSeconds > WorldController.WORLD_TICK_RATE)
                {
                    break;
                }
            }
        }

        #endregion


        #region CHUNK BUILDING

        public IEnumerator ProcessBuildChunkQueue()
        {
            BuildingChunkArea = true;

            _BuildChunkWorldTickLimiter.Restart();

            while (BuildChunkQueue.Count > 0)
            {
                _BuildChunkWorldTickLimiter.Restart();

                Vector3Int pos = BuildChunkQueue.Dequeue();

                if (!CheckChunkExistsAtPosition(pos) || GetChunkAtPosition(pos).Deactivated)
                {
                    CreateChunk(pos);
                }

                if (_BuildChunkWorldTickLimiter.Elapsed.TotalSeconds >= WorldController.WORLD_TICK_RATE)
                {
                    _BuildChunkWorldTickLimiter.Stop();
                    yield return null;
                }
            }

            BuildingChunkArea = false;
        }

        private void CreateChunk(Vector3Int position)
        {
            Chunk chunkAtPosition = GetChunkAtPosition(position);

            if ((chunkAtPosition != default) && !chunkAtPosition.Deactivated)
            {
                return;
            }

            Chunk chunk = _DeactivatedChunks.FirstOrDefault() ??
                          Instantiate(_ChunkObject, position, Quaternion.identity);
            chunk.Initialise(position);

            _DeactivatedChunks.Remove(chunk);
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