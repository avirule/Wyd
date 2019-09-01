#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Entity;
using Controllers.Game;
using Game;
using Game.Entity;
using Game.World.Chunk;
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
        private ObjectCache<Chunk> _ChunkCache;
        private Queue<Chunk> _DeactivationQueue;

        public Queue<Vector3> BuildChunkQueue;
        public int ActiveChunksCount => _Chunks.Count;
        public int CachedChunksCount => _ChunkCache.Size;
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

            _ChunkObject = Resources.Load<Chunk>(@"Prefabs/Chunk");
            _DeactivationQueue = new Queue<Chunk>();
            _FrameTimeLimiter = new Stopwatch();
            _Chunks = new Dictionary<Vector3, Chunk>();
            BuildChunkQueue = new Queue<Vector3>();
        }

        private void Start()
        {
            _ChunkCache = new ObjectCache<Chunk>(DeactivateChunk, chunk => Destroy(chunk.gameObject), false,
                OptionsController.Current.MaximumChunkCacheSize);

            PlayerController.Current.RegisterEntityChangedSubscriber(this);

            if (OptionsController.Current.PreInitializeChunkCache)
            {
                for (int i = 0; i < (OptionsController.Current.MaximumChunkCacheSize / 2); i++)
                {
                    Chunk chunk = Instantiate(_ChunkObject, Vector3.zero, Quaternion.identity,
                        WorldController.Current.transform);

                    _ChunkCache.CacheItem(ref chunk);
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

                Chunk chunk = _ChunkCache.RetrieveItem();

                if (chunk == default)
                {
                    chunk = Instantiate(_ChunkObject, position, Quaternion.identity, transform);
                }
                else
                {
                    chunk.Activate(position);
                }

                try
                {
                    AddChunk(chunk);
                }
                catch (Exception)
                {
                }

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
                _ChunkCache.CacheItem(ref chunk);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }
        }

        private Chunk DeactivateChunk(Chunk chunk)
        {
            RemoveChunk(chunk);
            FlagNeighborsPendingUpdate(chunk.Position);
            chunk.Deactivate();

            return chunk;
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

        public ushort GetBlockAtPosition(Vector3 position)
        {
            Vector3 chunkPosition = GetWorldChunkOriginFromGlobalPosition(position).ToInt();

            Chunk chunk = GetChunkAtPosition(chunkPosition);

            if ((chunk == default) || !chunk.Built)
            {
                return default;
            }

            return chunk.GetBlockAtPosition(position);
        }

        public static Vector3 GetWorldChunkOriginFromGlobalPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        #endregion
    }
}