#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.State;
using Game;
using Game.Entities;
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
    public class WorldController : SingletonController<WorldController>
    {
        private ChunkController _ChunkControllerObject;
        private Dictionary<Vector3, ChunkController> _Chunks;
        private ObjectCache<ChunkController> _ChunkCache;
        private Stack<IEntity> _BuildChunkAroundEntityStack;
        private Stopwatch _FrameTimer;
        private Vector3 _SpawnPoint;

        public CollisionLoaderController CollisionLoaderController;
        public WorldGenerationSettings WorldGenerationSettings;
        public float TicksPerSecond;

        public int ChunksActiveCount => _Chunks.Count;
        public int ChunksCachedCount => _ChunkCache.Size;
        public int ChunksQueuedForBuilding => _BuildChunkAroundEntityStack.Count;
        public bool AllChunksBuilt => _Chunks.All(kvp => kvp.Value.Built);
        public bool AllChunksMeshed => _Chunks.All(kvp => kvp.Value.Meshed);

        public long InitialTick { get; private set; }
        public TimeSpan WorldTickRate { get; private set; }

        /// <summary>
        ///     X,Z point in the world for spawning the player.
        /// </summary>
        public Vector3 SpawnPoint
        {
            get => _SpawnPoint;
            private set => _SpawnPoint = value;
        }

        public event EventHandler<ChunkChangedEventArgs> ChunkBlocksChanged;
        public event EventHandler<ChunkChangedEventArgs> ChunkMeshChanged;

        private void Awake()
        {
            if (GameController.Current == default)
            {
                SceneManager.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single);
            }

            AssignCurrent(this);
            SetTickRate();

            _ChunkControllerObject = Resources.Load<ChunkController>(@"Prefabs/Chunk");
            _Chunks = new Dictionary<Vector3, ChunkController>();
            _ChunkCache = new ObjectCache<ChunkController>(DeactivateChunk, chunk => Destroy(chunk.gameObject));
            _BuildChunkAroundEntityStack = new Stack<IEntity>();
            _FrameTimer = new Stopwatch();

#if UNITY_EDITOR
            WorldGenerationSettings.Radius = 5;
#endif
        }

        private void Start()
        {
            EntityController.Current.RegisterWatchForTag(RegisterCollideableEntity, "collider");
            EntityController.Current.RegisterWatchForTag(RegisterLoaderEntity, "loader");
            _ChunkCache.MaximumSize = OptionsController.Current.MaximumChunkCacheSize;
            // todo fix spawn point to set to useful value
            (_SpawnPoint.x, _SpawnPoint.y, _SpawnPoint.z) =
                Mathv.GetVector3IntIndex(WorldGenerationSettings.Seed,
                    new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue));


            if (!OptionsController.Current.PreInitializeChunkCache)
            {
                InitialiseChunkCache();
            }
        }

        private void Update()
        {
            _FrameTimer.Restart();

            if (_BuildChunkAroundEntityStack.Count > 0)
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
            while (_BuildChunkAroundEntityStack.Count > 0)
            {
                IEntity loader = _BuildChunkAroundEntityStack.Pop();
                int radius = loader.Tags.Contains("player")
                    ? /* todo create a chunk load radius option */
                    WorldGenerationSettings.Radius + OptionsController.Current.PreLoadChunkDistance
                    : 2;

                for (int x = -radius; x < (radius + 1); x++)
                {
                    for (int z = -radius; z < (radius + 1); z++)
                    {
                        Vector3 position = loader.CurrentChunk + new Vector3(x, 0f, z).Multiply(ChunkController.Size);

                        if (ChunkExistsAt(position))
                        {
                            continue;
                        }

                        ChunkController chunkController = _ChunkCache.RetrieveItem();

                        if (chunkController == default)
                        {
                            chunkController = Instantiate(_ChunkControllerObject, position, Quaternion.identity,
                                transform);
                            chunkController.BlocksChanged += OnChunkBlocksChanged;
                            chunkController.MeshChanged += OnChunkMeshChanged;
                            chunkController.DeactivationCallback += OnChunkDeactivationCallback;
                        }
                        else
                        {
                            chunkController.Activate(position);
                        }

                        _Chunks.Add(chunkController.Position, chunkController);
                        chunkController.AssignLoader(loader);

                        // ensures that neighbours update their meshes to cull newly out of sight faces
                        FlagNeighborsForMeshUpdate(chunkController.Position);
                    }
                }
                
                if (IsOnBorrowedUpdateTime())
                {
                    break;
                }
            }
        }

        private void FlagNeighborsForMeshUpdate(Vector3 chunkPosition)
        {
            FlagChunkForUpdateMesh(chunkPosition + (Vector3.forward * ChunkController.Size.z));
            FlagChunkForUpdateMesh(chunkPosition + (Vector3.right * ChunkController.Size.x));
            FlagChunkForUpdateMesh(chunkPosition + (Vector3.back * ChunkController.Size.z));
            FlagChunkForUpdateMesh(chunkPosition + (Vector3.left * ChunkController.Size.x));
        }

        private void FlagChunkForUpdateMesh(Vector3 chunkPosition)
        {
            if (!TryGetChunkAt(chunkPosition, out ChunkController chunk) || chunk.UpdateMesh)
            {
                return;
            }

            chunk.UpdateMesh = true;
        }

        #endregion


        #region CHUNK DISABLING

        private void CacheChunk(Vector3 chunkPosition)
        {
            if (!_Chunks.TryGetValue(chunkPosition, out ChunkController chunk))
            {
                return;
            }

            // Chunk is automatically deactivated by ObjectCache
            _ChunkCache.CacheItem(ref chunk);
        }

        private ChunkController DeactivateChunk(ChunkController chunkController)
        {
            if (!_Chunks.ContainsKey(chunkController.Position))
            {
                return default;
            }

            _Chunks.Remove(chunkController.Position);
            chunkController.Deactivate();

            return chunkController;
        }

        #endregion


        #region Event Invocators

        public void RegisterCollideableEntity(IEntity attachTo)
        {
            if ((CollisionLoaderController == default) || !attachTo.Tags.Contains("collider"))
            {
                return;
            }

            CollisionLoaderController.RegisterEntity(attachTo.Transform, 5);
        }

        public void RegisterLoaderEntity(IEntity loader)
        {
            if (!loader.Tags.Contains("loader"))
            {
                return;
            }

            loader.ChunkPositionChanged += (sender, vector3) => { _BuildChunkAroundEntityStack.Push(loader); };
            _BuildChunkAroundEntityStack.Push(loader);
        }

        private void OnChunkBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            if (args.ShouldUpdateNeighbors)
            {
                FlagNeighborsForMeshUpdate(args.ChunkBounds.min);
            }

            ChunkBlocksChanged?.Invoke(sender, args);
        }

        private void OnChunkMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            if (args.ShouldUpdateNeighbors)
            {
                FlagNeighborsForMeshUpdate(args.ChunkBounds.min);
            }

            ChunkMeshChanged?.Invoke(sender, args);
        }

        private void OnChunkDeactivationCallback(object sender, ChunkChangedEventArgs args)
        {
            CacheChunk(args.ChunkBounds.min);

            if (args.ShouldUpdateNeighbors)
            {
                FlagNeighborsForMeshUpdate(args.ChunkBounds.min);
            }
        }

        #endregion


        #region GET / EXISTS

        public bool ChunkExistsAt(Vector3 position)
        {
            return _Chunks.ContainsKey(position);
        }

        // todo this function needs to be made thread-safe
        public ChunkController GetChunkAt(Vector3 position)
        {
            bool trySuccess = _Chunks.TryGetValue(position, out ChunkController chunk);

            return trySuccess ? chunk : default;
        }

        public bool TryGetChunkAt(Vector3 position, out ChunkController chunkController)
        {
            return _Chunks.TryGetValue(position, out chunkController);
        }

        public Block GetBlockAt(Vector3 position)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(position);

            ChunkController chunkController = GetChunkAt(chunkPosition);

            if (chunkController == default)
            {
                throw new ArgumentOutOfRangeException(
                    $"Position `{position}` outside of current loaded radius.");
            }

            return chunkController.GetBlockAt(position);
        }

        public bool TryGetBlockAt(Vector3 position, out Block block)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(position);

            if (!TryGetChunkAt(chunkPosition, out ChunkController chunk) || !chunk.TryGetBlockAt(position, out block))
            {
                block = default;
                return false;
            }

            return true;
        }

        public bool BlockExistsAt(Vector3 position)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(position);

            if (!TryGetChunkAt(chunkPosition, out ChunkController chunk) || !chunk.Built)
            {
                return false;
            }

            return chunk.BlockExistsAt(position);
        }

        public void PlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(globalPosition);

            if (!TryGetChunkAt(chunkPosition, out ChunkController chunk))
            {
                throw new ArgumentOutOfRangeException($"Chunk containing position {globalPosition} does not exist.");
            }

            chunk.PlaceBlockAt(globalPosition, id);
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(globalPosition);

            return TryGetChunkAt(chunkPosition, out ChunkController chunk)
                   && chunk.TryPlaceBlockAt(globalPosition, id);
        }

        public void RemoveBlockAt(Vector3 globalPosition)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(globalPosition);

            if (!TryGetChunkAt(chunkPosition, out ChunkController chunk))
            {
                throw new ArgumentOutOfRangeException($"Chunk containing position {globalPosition} does not exist.");
            }

            chunk.RemoveBlockAt(globalPosition);
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition)
        {
            Vector3 chunkPosition = GetChunkOriginFromPosition(globalPosition);

            return TryGetChunkAt(chunkPosition, out ChunkController chunk)
                   && chunk.TryRemoveBlockAt(globalPosition);
        }

        public static Vector3 GetChunkOriginFromPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(ChunkController.Size).Floor().Multiply(ChunkController.Size);
        }

        public bool AreNeighborsBuilt(Vector3 position)
        {
            bool northBuilt = GetChunkAt(position + (Vector3.forward * ChunkController.Size.z))?.Built ?? true;
            bool eastBuilt = GetChunkAt(position + (Vector3.right * ChunkController.Size.x))?.Built ?? true;
            bool southBuilt = GetChunkAt(position + (Vector3.back * ChunkController.Size.z))?.Built ?? true;
            bool westBuilt = GetChunkAt(position + (Vector3.left * ChunkController.Size.x))?.Built ?? true;

            return northBuilt && eastBuilt && southBuilt && westBuilt;
        }

        #endregion


        #region MISC

        private void InitialiseChunkCache()
        {
            for (int i = 0; i < (OptionsController.Current.MaximumChunkCacheSize / 2); i++)
            {
                ChunkController chunkController =
                    Instantiate(_ChunkControllerObject, Vector3.zero, Quaternion.identity, transform);

                _ChunkCache.CacheItem(ref chunkController);
            }
        }

        #endregion
    }
}
