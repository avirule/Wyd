#region

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Entity;
using Controllers.Game;
using Game.Entity;
using Game.World;
using UnityEngine;

#endregion

namespace Controllers.World
{
    public sealed class ChunkController : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        public static ChunkController Current;

        private Stopwatch _FrameTimeLimiter;
        private Chunk _ChunkObject;
        private Dictionary<Vector3, Chunk> _Chunks;
        private Queue<Chunk> _CachedChunks;
        private Queue<Chunk> _DeactivationQueue;

        public Queue<Vector3> BuildChunkQueue;
        public int ActiveChunksCount => _Chunks.Count;
        public int CachedChunksCount => _CachedChunks.Count;
        public bool AllChunksBuilt => _Chunks.All(kvp => kvp.Value.Built);
        public bool AllChunksMeshed => _Chunks.All(kvp => kvp.Value.Meshed);

        public bool EntityChangedChunk { get; set; }

        private void Awake()
        {
            if ((Current != null) && (Current != this))
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }

            _ChunkObject = Resources.Load<Chunk>(@"Prefabs\Chunk");
            _CachedChunks = new Queue<Chunk>();
            _DeactivationQueue = new Queue<Chunk>();
            _FrameTimeLimiter = new Stopwatch();

            _Chunks = new Dictionary<Vector3, Chunk>();
            BuildChunkQueue = new Queue<Vector3>();
        }

        private void Start()
        {
            PlayerController.Current.RegisterEntityChangedSubscriber(this);

            if (OptionsController.Current.PreInitializeChunkCache)
            {
                for (int i = 0; i < (OptionsController.Current.MaximumChunkCacheSize / 2); i++)
                {
                    Chunk chunk = Instantiate(_ChunkObject, Vector3.zero, Quaternion.identity,
                        WorldController.Current.transform);
                    chunk.Deactivate();

                    _CachedChunks.Enqueue(chunk);
                }
            }
        }

        private void Update()
        {
            _FrameTimeLimiter.Restart();

            if (OptionsController.Current.ThreadingMode == ThreadingMode.Variable)
            {
                ModifyThreadedExecutionQueueThreadingMode();
            }

            if (EntityChangedChunk)
            {
                MarkOutOfBoundsChunksForDeactivation(PlayerController.Current.CurrentChunk);

                EntityChangedChunk = false;
            }

            if (BuildChunkQueue.Count > 0)
            {
                ProcessBuildChunkQueue();
            }

            if (_DeactivationQueue.Count > 0)
            {
                ProcessDeactivationQueue();
            }

            // if maximum chunk cache size is not zero then cull chunks down to half the maximum when idle
            if ((OptionsController.Current.MaximumChunkCacheSize != 0) &&
                (_CachedChunks.Count > (OptionsController.Current.MaximumChunkCacheSize / 2)))
            {
                CullCachedChunks();
            }

            _FrameTimeLimiter.Stop();
        }

        private void AddChunk(Chunk chunk)
        {
            _Chunks.Add(chunk.Position, chunk);
        }

        private void RemoveChunk(Chunk chunk)
        {
            if (!_Chunks.ContainsKey(chunk.Position))
            {
                return;
            }

            _Chunks.Remove(chunk.Position);
        }

        private static void ModifyThreadedExecutionQueueThreadingMode()
        {
            // todo something where this isn't local const. Relative to max internal frame time maybe?
            const float fps60 = 1f / 60f;

            if (Chunk.ThreadedExecutionQueue.MultiThreadedExecution && (Time.deltaTime > fps60))
            {
                Chunk.ThreadedExecutionQueue.MultiThreadedExecution = false;
            }
            else if (!Chunk.ThreadedExecutionQueue.MultiThreadedExecution && (Time.deltaTime <= fps60))
            {
                Chunk.ThreadedExecutionQueue.MultiThreadedExecution = true;
            }
        }

        #region CHUNK BUILDING

        public void ProcessBuildChunkQueue()
        {
            while (BuildChunkQueue.Count > 0)
            {
                Vector3 position = BuildChunkQueue.Dequeue();

                if (CheckChunkExistsAtPosition(position))
                {
                    continue;
                }

                Chunk chunk;

                if (_CachedChunks.Count > 0)
                {
                    chunk = _CachedChunks.Dequeue();
                    chunk.Activate(position);
                }
                else
                {
                    chunk = Instantiate(_ChunkObject, position, Quaternion.identity, transform);
                }

                AddChunk(chunk);

                // ensures that neighbours update their meshes to cull newly out of sight faces
                FlagNeighborsPendingUpdate(chunk.Position);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }
        }

        private void FlagNeighborsPendingUpdate(Vector3 position)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0)
                {
                    continue;
                }

                Vector3 modifiedPosition = position + new Vector3(x * Chunk.Size.x, 0, 0);
                Chunk chunkAtPosition = GetChunkAtPosition(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.PendingMeshUpdate || !chunkAtPosition.Active)
                {
                    continue;
                }

                chunkAtPosition.PendingMeshUpdate = true;
            }

            for (int z = -1; z <= 1; z++)
            {
                if (z == 0)
                {
                    continue;
                }

                Vector3 modifiedPosition = position + new Vector3(0, 0, z * Chunk.Size.z);
                Chunk chunkAtPosition = GetChunkAtPosition(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.PendingMeshUpdate || !chunkAtPosition.Active)
                {
                    continue;
                }

                chunkAtPosition.PendingMeshUpdate = true;
            }
        }

        #endregion


        #region CHUNK DISABLING

        private void MarkOutOfBoundsChunksForDeactivation(Vector3Int chunkPosition)
        {
            foreach (KeyValuePair<Vector3, Chunk> kvp in _Chunks)
            {
                Vector3 difference = (kvp.Value.Position - chunkPosition).Abs();

                if ((difference.x <= ((WorldController.Current.WorldGenerationSettings.Radius + 1) * Chunk.Size.x)) &&
                    (difference.z <= ((WorldController.Current.WorldGenerationSettings.Radius + 1) * Chunk.Size.z)))
                {
                    continue;
                }

                _DeactivationQueue.Enqueue(kvp.Value);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }
        }

        private void ProcessDeactivationQueue()
        {
            while (_DeactivationQueue.Count > 0)
            {
                Chunk chunk = _DeactivationQueue.Dequeue();
                DeactivateChunk(chunk);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }
        }

        private void DeactivateChunk(Chunk chunk)
        {
            _CachedChunks.Enqueue(chunk);
            RemoveChunk(chunk);
            FlagNeighborsPendingUpdate(chunk.Position);
            chunk.Deactivate();
        }

        private void CullCachedChunks()
        {
            if (OptionsController.Current.MaximumChunkCacheSize == 0)
            {
                return;
            }

            // controller will cull chunks down to half the maximum when idle
            while (_CachedChunks.Count > (OptionsController.Current.MaximumChunkCacheSize / 2))
            {
                Chunk chunk = _CachedChunks.Dequeue();
                Destroy(chunk.gameObject);

                // continue culling if the amount of cached chunks is greater than the maximum
                if ((_FrameTimeLimiter.Elapsed.TotalSeconds >
                     OptionsController.Current.MaximumInternalFrameTime) &&
                    (OptionsController.Current.ChunkCacheCullingAggression == CacheCullingAggression.Passive))
                {
                    break;
                }
            }
        }

        #endregion


        #region MISC

        public bool CheckChunkExistsAtPosition(Vector3 position)
        {
            return _Chunks.ContainsKey(position);
        }

        // todo this function needs to be made thread-safe
        public Chunk GetChunkAtPosition(Vector3 position)
        {
            bool trySuccess = _Chunks.TryGetValue(position, out Chunk chunk);

            return trySuccess ? chunk : default;
        }

        public ushort GetIdAtPosition(Vector3 position)
        {
            Vector3 chunkPosition = WorldController.GetWorldChunkOriginFromGlobalPosition(position).ToInt();

            Chunk chunk = GetChunkAtPosition(chunkPosition);

            if ((chunk == default) || !chunk.Built)
            {
                return default;
            }

            Vector3 localPosition = (position - chunkPosition).Abs();
            int localPosition1d = localPosition.To1D(Chunk.Size);

            if (chunk.Blocks.Length <= localPosition1d)
            {
                return default;
            }

            return chunk.Blocks[localPosition1d];
        }

        #endregion
    }
}