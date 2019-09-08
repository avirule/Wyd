#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Entity;
using Controllers.Game;
using Game;
using Game.Entity;
using Game.World;
using Game.World.Blocks;
using Game.World.Chunks;
using Logging;
using NLog;
using UnityEngine;
using UnityEngine.SceneManagement;

// ReSharper disable UnusedAutoPropertyAccessor.Global

#endregion

namespace Controllers.World
{
    public class WorldController : SingletonController<WorldController>, IEntityChunkChangedSubscriber
    {
        private Chunk _ChunkObject;
        private Dictionary<Vector3, Chunk> _Chunks;
        private ObjectCache<Chunk> _ChunkCache;
        private Queue<Vector3> _BuildChunkQueue;
        private Stopwatch _FrameTimer;

        public CollisionTokenController CollisionTokenController;
        public WorldGenerationSettings WorldGenerationSettings;
        public float TicksPerSecond;

        public int ChunksActiveCount => _Chunks.Count;
        public int ChunksCachedCount => _ChunkCache.Size;
        public int ChunksQueuedForBuilding => _BuildChunkQueue.Count;
        public bool AllChunksBuilt => _Chunks.All(kvp => kvp.Value.Built);
        public bool AllChunksMeshed => _Chunks.All(kvp => kvp.Value.Meshed);

        public long InitialTick { get; private set; }
        public TimeSpan WorldTickRate { get; private set; }
        public bool PrimaryLoaderChangedChunk { get; set; }

        private void Awake()
        {
            if (GameController.Current == default)
            {
                SceneManager.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single);
            }

            AssignCurrent(this);
            SetTickRate();

            _ChunkObject = Resources.Load<Chunk>(@"Prefabs/Chunk");
            _Chunks = new Dictionary<Vector3, Chunk>();
            _ChunkCache = new ObjectCache<Chunk>(DeactivateChunk, chunk => Destroy(chunk.gameObject));
            _BuildChunkQueue = new Queue<Vector3>();
            _FrameTimer = new Stopwatch();

#if UNITY_EDITOR
            WorldGenerationSettings.Radius = 5;
#endif
        }

        private void Start()
        {
            _ChunkCache.MaximumSize = OptionsController.Current.MaximumChunkCacheSize;
            PlayerController.Current.RegisterEntityChangedSubscriber(this);

            if (!OptionsController.Current.PreInitializeChunkCache)
            {
                InitialiseChunkCache();
            }
        }

        private void Update()
        {
            _FrameTimer.Restart();

            if (PrimaryLoaderChangedChunk)
            {
                UpdateChunkLoadArea(PlayerController.Current.CurrentChunk);

                PrimaryLoaderChangedChunk = false;
            }

            if (_BuildChunkQueue.Count > 0)
            {
                ProcessBuildChunkQueue();
            }
        }


        #region TICKS / TIME

        private void SetTickRate()
        {
            if (TicksPerSecond < 1)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    "World tick rate cannot be set to less than 1tick/s. Exiting game.");
                GameController.ApplicationClose();
                return;
            }

            WorldTickRate = TimeSpan.FromSeconds(1d / TicksPerSecond);

            InitialTick = DateTime.Now.Ticks;
        }

        public bool IsOnBorrowedUpdateTime()
        {
            return _FrameTimer.Elapsed > OptionsController.Current.MaximumInternalFrameTime;
        }

        #endregion


        #region CHUNK BUILDING

        public void ProcessBuildChunkQueue()
        {
            while (_BuildChunkQueue.Count > 0)
            {
                Vector3 position = _BuildChunkQueue.Dequeue();

                if (ChunkExistsAt(position))
                {
                    continue;
                }

                Chunk chunk = _ChunkCache.RetrieveItem();

                if (chunk == default)
                {
                    chunk = Instantiate(_ChunkObject, position, Quaternion.identity, transform);
                    chunk.DeactivationCallback += (sender, chunkPosition) => { CacheChunk(chunkPosition); };
                }
                else
                {
                    chunk.Activate(position);
                }

                if (!_Chunks.ContainsKey(chunk.Position))
                {
                    _Chunks.Add(chunk.Position, chunk);
                }

                // ensures that neighbours update their meshes to cull newly out of sight faces
                FlagNeighborsPendingUpdate(chunk.Position);

                if (IsOnBorrowedUpdateTime())
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
                Chunk chunkAtPosition = GetChunkAt(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.UpdateMesh || !chunkAtPosition.Active)
                {
                    continue;
                }

                chunkAtPosition.UpdateMesh = true;
            }

            for (int z = -1; z <= 1; z++)
            {
                if (z == 0)
                {
                    continue;
                }

                Vector3 modifiedPosition = position + new Vector3(0, 0, z * Chunk.Size.z);
                Chunk chunkAtPosition = GetChunkAt(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.UpdateMesh || !chunkAtPosition.Active)
                {
                    continue;
                }

                chunkAtPosition.UpdateMesh = true;
            }
        }

        #endregion


        #region CHUNK DISABLING

        private void CacheChunk(Vector3 chunkPosition)
        {
            if (!_Chunks.TryGetValue(chunkPosition, out Chunk chunk))
            {
                return;
            }

            // Chunk is automatically deactivated by ObjectCache
            _ChunkCache.CacheItem(ref chunk);
        }

        private Chunk DeactivateChunk(Chunk chunk)
        {
            if (!_Chunks.ContainsKey(chunk.Position))
            {
                return default;
            }

            _Chunks.Remove(chunk.Position);
            FlagNeighborsPendingUpdate(chunk.Position);
            chunk.Deactivate();

            return chunk;
        }

        #endregion


        #region ON EVENT

        private void UpdateChunkLoadArea(Vector3 chunkPosition)
        {
            EnqueueBuildChunkArea(chunkPosition,
                WorldGenerationSettings.Radius + OptionsController.Current.PreLoadChunkDistance);
        }

        public void EnqueueBuildChunkArea(Vector3 origin, int radius)
        {
            // +1 to include player's chunk
            for (int x = -radius; x < (radius + 1); x++)
            {
                for (int z = -radius; z < (radius + 1); z++)
                {
                    Vector3 position = origin + new Vector3(x, 0, z).Multiply(Chunk.Size);

                    _BuildChunkQueue.Enqueue(position);
                }
            }
        }

        #endregion


        #region GET / EXISTS

        public bool ChunkExistsAt(Vector3 position)
        {
            return _Chunks.ContainsKey(position);
        }

        // todo this function needs to be made thread-safe
        public Chunk GetChunkAt(Vector3 position)
        {
            bool trySuccess = _Chunks.TryGetValue(position, out Chunk chunk);

            return trySuccess ? chunk : default;
        }

        public Block GetBlockAt(Vector3 position)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(position);

            Chunk chunk = GetChunkAt(chunkPosition);

            if ((chunk == default) || !chunk.Built)
            {
                return default;
            }

            return chunk.GetBlockAt(position);
        }

        public bool BlockExistsAt(Vector3 position)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(position);

            Chunk chunk = GetChunkAt(chunkPosition);

            if ((chunk == default) || !chunk.Built)
            {
                return default;
            }

            return chunk.BlockExistsAt(position);
        }

        public static Vector3 GetChunkOriginFromPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        public bool AreNeighborsBuilt(Vector3 position)
        {
            bool northBuilt = GetChunkAt(position + (Vector3.forward * Chunk.Size.z))?.Built ?? true;
            bool eastBuilt = GetChunkAt(position + (Vector3.right * Chunk.Size.x))?.Built ?? true;
            bool southBuilt = GetChunkAt(position + (Vector3.back * Chunk.Size.z))?.Built ?? true;
            bool westBuilt = GetChunkAt(position + (Vector3.left * Chunk.Size.x))?.Built ?? true;

            return northBuilt && eastBuilt && southBuilt && westBuilt;
        }

        #endregion


        #region MISC

        private void InitialiseChunkCache()
        {
            for (int i = 0; i < (OptionsController.Current.MaximumChunkCacheSize / 2); i++)
            {
                Chunk chunk = Instantiate(_ChunkObject, Vector3.zero, Quaternion.identity, transform);

                _ChunkCache.CacheItem(ref chunk);
            }
        }

        public void RegisterEntity(Transform attachTo, int loadRadius)
        {
            if (CollisionTokenController == default)
            {
                return;
            }

            CollisionTokenController.RegisterEntity(attachTo, loadRadius);
        }

        #endregion
    }
}