#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private ChunkRegionController _ChunkRegionControllerObject;
        private Dictionary<Vector3, ChunkRegionController> _ChunkRegions;
        private ObjectCache<ChunkRegionController> _ChunkCache;
        private Stack<IEntity> _BuildChunkAroundEntityStack;
        private WorldSaveFileProvider _SaveFileProvider;
        private Stopwatch _FrameTimer;
        private Vector3 _SpawnPoint;

        public CollisionLoaderController CollisionLoaderController;
        public string SeedString;
        public float TicksPerSecond;

        public int ChunkRegionsActiveCount => _ChunkRegions.Count;
        public int ChunkRegionsCachedCount => _ChunkCache.Size;
        public int ChunkRegionsQueuedForCreation => _BuildChunkAroundEntityStack.Count;

        public WorldSeed Seed { get; private set; }
        public long InitialTick { get; private set; }
        public TimeSpan WorldTickRate { get; private set; }
        public Material TerrainMaterial { get; private set; }

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

        #region DEBUG

#if UNITY_EDITOR

        public bool IgnoreInternalFrameLimit;
        public bool StepIntoSelectedChunkStep;
        public ChunkGenerationDispatcher.GenerationStep SelectedStep;

#endif

        #endregion

        private void Awake()
        {
            if (GameController.Current == default)
            {
                SceneManager.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single);
            }

            AssignCurrent(this);
            SetTickRate();

            _ChunkRegionControllerObject = Resources.Load<ChunkRegionController>(@"Prefabs/Chunk");
            _ChunkRegions = new Dictionary<Vector3, ChunkRegionController>();
            _ChunkCache = new ObjectCache<ChunkRegionController>(false, false, -1, DeactivateChunk, DestroyCulledChunk);
            _BuildChunkAroundEntityStack = new Stack<IEntity>();
            _SaveFileProvider = new WorldSaveFileProvider("world");
            _FrameTimer = new Stopwatch();

            Seed = new WorldSeed(SeedString);
            _SaveFileProvider.Initialise().ConfigureAwait(false);
        }

        private void Start()
        {
            TerrainMaterial = Resources.Load<Material>(@"Materials\TerrainMaterial");
            TerrainMaterial.SetTexture(TextureController.MainTexPropertyID,
                TextureController.Current.TerrainTexture);

            EntityController.Current.RegisterWatchForTag(RegisterCollideableEntity, "collider");
            EntityController.Current.RegisterWatchForTag(RegisterLoaderEntity, "loader");
            _ChunkCache.MaximumSize = OptionsController.Current.MaximumChunkCacheSize;
            // todo fix spawn point to set to useful value
            (_SpawnPoint.x, _SpawnPoint.y, _SpawnPoint.z) =
                Mathv.GetIndexAs3D(Seed, new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue));


            if (OptionsController.Current.PreInitializeChunkCache)
            {
                InitialiseChunkCache();
            }
        }

        private void Update()
        {
            _FrameTimer.Restart();
        }

        private void LateUpdate()
        {
            if (_BuildChunkAroundEntityStack.Count > 0)
            {
                ProcessBuildChunkQueue();
            }
        }

        private void OnApplicationQuit()
        {
            WorldSaveFileProvider.ApplicationQuit();
            _SaveFileProvider.Dispose();
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

        public bool IsInSafeFrameTime() => _FrameTimer.Elapsed <= OptionsController.Current.MaximumInternalFrameTime;

        public void GetRemainingSafeFrameTime(out TimeSpan remainingTime)
        {
            remainingTime = OptionsController.Current.MaximumInternalFrameTime - _FrameTimer.Elapsed;
        }

        #endregion


        #region CHUNK BUILDING

        public void ProcessBuildChunkQueue()
        {
            while ((_BuildChunkAroundEntityStack.Count > 0) && IsInSafeFrameTime())
            {
                IEntity loader = _BuildChunkAroundEntityStack.Pop();
                int radius = loader.Tags.Contains("player")
                    ? OptionsController.Current.RenderDistance + OptionsController.Current.PreLoadChunkDistance
                    : 1;

                for (int x = -radius; x < (radius + 1); x++)
                {
                    for (int z = -radius; z < (radius + 1); z++)
                    {
                        if (!IsInSafeFrameTime())
                        {
                            _BuildChunkAroundEntityStack.Push(loader);
                            return;
                        }

                        Vector3 position = loader.CurrentChunk
                                           + new Vector3(x, 0f, z).Multiply(ChunkRegionController.Size);

                        if (ChunkExistsAt(position))
                        {
                            continue;
                        }

                        if (!_ChunkCache.TryRetrieveItem(out ChunkRegionController chunkController))
                        {
                            chunkController = Instantiate(_ChunkRegionControllerObject, position, Quaternion.identity,
                                transform);
                        }
                        else
                        {
                            chunkController.Activate(position);
                        }

                        chunkController.BlocksChanged += OnChunkBlocksChanged;
                        chunkController.MeshChanged += OnChunkMeshChanged;
                        chunkController.DeactivationCallback += OnChunkDeactivationCallback;

                        chunkController.AssignLoader(loader);
                        _ChunkRegions.Add(chunkController.Position, chunkController);

                        // ensures that neighbours update their meshes to cull newly out of sight faces
                        FlagNeighborsForMeshUpdate(chunkController.Position, Directions.CardinalDirectionsVector3);
                    }
                }
            }
        }

        public void FlagNeighborsForMeshUpdate(Vector3 globalChunkPosition, IEnumerable<Vector3> directions)
        {
            foreach (Vector3 normal in directions)
            {
                FlagChunkForUpdateMesh(globalChunkPosition + normal.Multiply(Chunk.Size));
            }
        }

        public void FlagChunkForUpdateMesh(Vector3 globalChunkPosition)
        {
            if (TryGetChunkAt(globalChunkPosition.RoundBy(ChunkRegionController.Size),
                out ChunkRegionController chunkController))
            {
                chunkController.RequestMeshUpdate(globalChunkPosition);
            }
        }

        #endregion


        #region CHUNK DISABLING

        private void CacheChunk(Vector3 chunkRegionPosition)
        {
            if (!_ChunkRegions.TryGetValue(chunkRegionPosition, out ChunkRegionController chunkController))
            {
                return;
            }

            chunkController.BlocksChanged -= OnChunkBlocksChanged;
            chunkController.MeshChanged -= OnChunkMeshChanged;
            chunkController.DeactivationCallback -= OnChunkDeactivationCallback;

            // Chunk is automatically deactivated by ObjectCache
            _ChunkCache.CacheItem(ref chunkController);
        }

        private static ref ChunkRegionController DeactivateChunk(ref ChunkRegionController chunkRegionController)
        {
            //GeneralExecutionJob job = new GeneralExecutionJob(() =>
            //{
            //    if (!_Chunks.ContainsKey(chunkController.Position))
            //    {
            //        return;
            //    }

            //    FlagNeighborsForMeshUpdate(chunkController.Position,
            //        Directions.CardinalDirectionsVector3);
            //    _SaveFileProvider.CompressAndCommit(chunkController.Position,
            //        chunkController.Serialize());

            //    _Chunks.Remove(chunkController.Position);
            //});

            if (chunkRegionController != default)
            {
                chunkRegionController.Deactivate();
            }

            //GameController.QueueJob(job);

            return ref chunkRegionController;
        }

        private static void DestroyCulledChunk(ref ChunkRegionController chunkRegionController)
        {
            Destroy(chunkRegionController.gameObject);
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
            FlagNeighborsForMeshUpdate(args.ChunkBounds.min, args.NeighborDirectionsToUpdate);

            ChunkBlocksChanged?.Invoke(sender, args);
        }

        private void OnChunkMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            FlagNeighborsForMeshUpdate(args.ChunkBounds.min, args.NeighborDirectionsToUpdate);

            ChunkMeshChanged?.Invoke(sender, args);
        }

        private void OnChunkDeactivationCallback(object sender, ChunkChangedEventArgs args)
        {
            CacheChunk(args.ChunkBounds.min);

            FlagNeighborsForMeshUpdate(args.ChunkBounds.min, args.NeighborDirectionsToUpdate);
        }

        #endregion


        #region GET / EXISTS

        public bool ChunkExistsAt(Vector3 position) => _ChunkRegions.ContainsKey(position);

        public ChunkRegionController GetChunkAt(Vector3 position)
        {
            bool trySuccess = _ChunkRegions.TryGetValue(position, out ChunkRegionController chunkController);

            return trySuccess ? chunkController : default;
        }

        public bool TryGetChunkAt(Vector3 position, out ChunkRegionController chunkRegionController) =>
            _ChunkRegions.TryGetValue(position, out chunkRegionController);

        public ref Block GetBlockAt(Vector3 globalPosition)
        {
            Vector3 chunkRegionPosition = globalPosition.RoundBy(ChunkRegionController.Size);

            ChunkRegionController chunkRegionController = GetChunkAt(chunkRegionPosition);

            if (chunkRegionController == default)
            {
                throw new ArgumentOutOfRangeException(
                    $"Position `{globalPosition}` outside of current loaded radius.");
            }

            return ref chunkRegionController.GetBlockAt(globalPosition);
        }

        public bool TryGetBlockAt(Vector3 globalPosition, out Block block)
        {
            block = default;
            Vector3 chunkRegionPosition = globalPosition.RoundBy(ChunkRegionController.Size);

            return TryGetChunkAt(chunkRegionPosition, out ChunkRegionController chunkController)
                   && (chunkController != default)
                   && chunkController.TryGetBlockAt(globalPosition, out block);
        }

        public bool BlockExistsAt(Vector3 globalPosition)
        {
            Vector3 chunkRegionPosition = globalPosition.RoundBy(ChunkRegionController.Size);

            return TryGetChunkAt(chunkRegionPosition, out ChunkRegionController chunkController)
                   && chunkController.BlockExistsAt(globalPosition);
        }

        public void PlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            Vector3 chunkRegionPosition = globalPosition.RoundBy(ChunkRegionController.Size);

            if (!TryGetChunkAt(chunkRegionPosition, out ChunkRegionController chunkController))
            {
                throw new ArgumentOutOfRangeException($"Chunk containing position {globalPosition} does not exist.");
            }

            chunkController.ImmediatePlaceBlockAt(globalPosition, id);
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            Vector3 chunkRegionPosition = globalPosition.RoundBy(ChunkRegionController.Size);

            return TryGetChunkAt(chunkRegionPosition, out ChunkRegionController chunkController)
                   && (chunkController != default)
                   && chunkController.TryPlaceBlockAt(globalPosition, id);
        }

        public void RemoveBlockAt(Vector3 globalPosition)
        {
            Vector3 chunkRegionPosition = globalPosition.RoundBy(ChunkRegionController.Size);

            if (!TryGetChunkAt(chunkRegionPosition, out ChunkRegionController chunkController))
            {
                throw new ArgumentOutOfRangeException($"Chunk containing position {globalPosition} does not exist.");
            }

            chunkController.ImmediateRemoveBlockAt(globalPosition);
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition)
        {
            Vector3 chunkRegionPosition = globalPosition.RoundBy(ChunkRegionController.Size);

            return TryGetChunkAt(chunkRegionPosition, out ChunkRegionController chunkController)
                   && (chunkController != default)
                   && chunkController.TryRemoveBlockAt(globalPosition);
        }

        public ChunkGenerationDispatcher.GenerationStep AggregateNeighborsStep(Vector3 position)
        {
            ChunkGenerationDispatcher.GenerationStep generationStep = ChunkGenerationDispatcher.GenerationStep.Complete;

            if (TryGetChunkAt(position + (Vector3.forward * ChunkRegionController.Size.z),
                out ChunkRegionController northChunk))
            {
                generationStep &= northChunk.AggregateGenerationStep;
            }

            if (TryGetChunkAt(position + (Vector3.right * ChunkRegionController.Size.x),
                out ChunkRegionController eastChunk))
            {
                generationStep &= eastChunk.AggregateGenerationStep;
            }

            if (TryGetChunkAt(position + (Vector3.back * ChunkRegionController.Size.z),
                out ChunkRegionController southChunk))
            {
                generationStep &= southChunk.AggregateGenerationStep;
            }

            if (TryGetChunkAt(position + (Vector3.left * ChunkRegionController.Size.x),
                out ChunkRegionController westChunk))
            {
                generationStep &= westChunk.AggregateGenerationStep;
            }

            return generationStep;
        }

        #endregion


        #region MISC

        private void InitialiseChunkCache()
        {
            for (int i = 0; i < (OptionsController.Current.MaximumChunkCacheSize / 2); i++)
            {
                ChunkRegionController chunkRegionController =
                    Instantiate(_ChunkRegionControllerObject, Vector3.zero, Quaternion.identity, transform);
                chunkRegionController.gameObject.SetActive(false);
                _ChunkCache.CacheItem(ref chunkRegionController);
            }
        }

        #endregion
    }
}
