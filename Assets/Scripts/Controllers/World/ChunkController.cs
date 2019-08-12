#region

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Game;
using Environment.Terrain;
using Logging;
using NLog;
using Static;
using UnityEngine;
using Debug = UnityEngine.Debug;

#endregion

namespace Controllers.World
{
    public sealed class ChunkController : MonoBehaviour
    {
        private Stopwatch _WorldTickLimiter;
        private Stopwatch _BuildChunkWorldTickLimiter;
        private Chunk _ChunkObject;
        private List<Chunk> _CachedChunks;
        private bool _ProcessingChunkQueue;

        public List<Chunk> Chunks;
        public Queue<Vector3Int> BuildChunkQueue;
        public int CurrentCacheSize => _CachedChunks.Count;
        public bool AllChunksGenerated => Chunks.All(chunk => chunk.Generated);
        public bool AllChunksMeshed => Chunks.All(chunk => chunk.Meshed);

        private void Awake()
        {
            _ChunkObject = Resources.Load<Chunk>(@"Environment\Terrain\Chunk");
            _CachedChunks = new List<Chunk>();
            _WorldTickLimiter = new Stopwatch();
            _BuildChunkWorldTickLimiter = new Stopwatch();
            _ProcessingChunkQueue = false;

            Chunks = new List<Chunk>();
            BuildChunkQueue = new Queue<Vector3Int>();
        }

        private void Update()
        {
            foreach (Chunk chunk in Chunks)
            {
                chunk.Tick();
            }
        }

        public void Tick(BoundsInt bounds)
        {
            _WorldTickLimiter.Restart();

            if ((BuildChunkQueue.Count > 0) && !_ProcessingChunkQueue)
            {
                StartCoroutine(ProcessBuildChunkQueue());
            }

            if (AllChunksGenerated)
            {
                DeactivateOutOfBoundsChunks(bounds);
            }

            // cull chunks down to half the maximum when idle
            if (_CachedChunks.Count > (GameController.SettingsController.MaximumChunkCacheSize / 2))
            {
                CullCachedChunks();
            }

            _WorldTickLimiter.Stop();
        }

        #region CHUNK DESTROYING

        private void DeactivateOutOfBoundsChunks(BoundsInt bounds)
        {
            int initialCount = Chunks.Count;

            for (int i = initialCount - 1; i >= 0; i--)
            {
                if (Mathv.ContainsVector3Int(bounds, Chunks[i].Position))
                {
                    continue;
                }

                DeactivateChunk(Chunks[i]);

                if (_WorldTickLimiter.Elapsed.TotalSeconds > WorldController.WorldTickRate)
                {
                    break;
                }
            }
        }

        private void DeactivateChunk(Chunk chunk)
        {
            _CachedChunks.Add(chunk);
            FlagNeighborsPendingUpdate(chunk.Position);
            chunk.enabled = false;
            Chunks.Remove(chunk);
        }

        private void CullCachedChunks()
        {
            bool hasIgnoredCancellation = false;

            // controller will cull chunks down to half the maximum when idle
            while (_CachedChunks.Count > (GameController.SettingsController.MaximumChunkCacheSize / 2))
            {
                Chunk chunk = _CachedChunks.First();
                _CachedChunks.Remove(chunk);
                Destroy(chunk);

                // continue culling if the amount of cached chunks is greater than the maximum
                if (_WorldTickLimiter.Elapsed.TotalSeconds < WorldController.WorldTickRate)
                {
                    continue;
                }

                if (_CachedChunks.Count > GameController.SettingsController.MaximumChunkCacheSize)
                {
                    // stops spam
                    if (!hasIgnoredCancellation)
                    {
                        EventLog.Logger.Log(LogLevel.Warn,
                            "Controller has cached too many chunks. Ignoring tick rate cancellation and continuing.");
                        hasIgnoredCancellation = true;
                    }

                    continue;
                }

                break;
            }
        }

        #endregion


        #region CHUNK BUILDING

        public IEnumerator ProcessBuildChunkQueue()
        {
            _ProcessingChunkQueue = true;
            _BuildChunkWorldTickLimiter.Reset();

            while (BuildChunkQueue.Count > 0)
            {
                _BuildChunkWorldTickLimiter.Start();

                Vector3Int position = BuildChunkQueue.Dequeue();

                if (!CheckChunkExistsAtPosition(position) || !GetChunkAtPosition(position).enabled)
                {
                    CreateChunk(position);
                }

                if (_BuildChunkWorldTickLimiter.Elapsed.TotalSeconds > WorldController.WorldTickRate)
                {
                    _BuildChunkWorldTickLimiter.Reset();
                    yield return null;
                }
            }

            _ProcessingChunkQueue = false;
        }

        private void CreateChunk(Vector3Int position)
        {
            Chunk chunkAtPosition = GetChunkAtPosition(position);

            if ((chunkAtPosition != default) && !chunkAtPosition.enabled)
            {
                chunkAtPosition.enabled = true;
                return;
            }

            Chunk chunk = _CachedChunks.FirstOrDefault();

            if (chunk == default)
            {
                chunk = Instantiate(_ChunkObject, position, Quaternion.identity);
            }
            else
            {
                _CachedChunks.Remove(chunk);
                chunk.transform.position = position;
            }

            Chunks.Add(chunk);
            chunk.enabled = true;

            // ensures that neighbours update their meshes to cull newly out of sight faces
            FlagNeighborsPendingUpdate(chunk.Position);
        }

        private void FlagNeighborsPendingUpdate(Vector3Int position)
        {
            for (int x = -1; x <= 1; x++)
            {
                Vector3Int modifiedPosition = position + new Vector3Int(x * Chunk.Size.x, 0, 0);
                Chunk chunkAtPosition = GetChunkAtPosition(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.PendingMeshUpdate)
                {
                    continue;
                }

                chunkAtPosition.PendingMeshUpdate = true;
            }

            for (int z = -1; z <= 1; z++)
            {
                Vector3Int modifiedPosition = position + new Vector3Int(0, 0, z * Chunk.Size.z);
                Chunk chunkAtPosition = GetChunkAtPosition(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.PendingMeshUpdate)
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

            if ((chunk == null) || !chunk.Generated)
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