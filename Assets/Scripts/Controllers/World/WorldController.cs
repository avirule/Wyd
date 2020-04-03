#region

using System;
using System.Collections;
using System.Collections.Generic;
using Serilog;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World.Chunk;
using Wyd.Game;
using Wyd.Game.Entities;
using Wyd.Game.World;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Collections;

// ReSharper disable UnusedAutoPropertyAccessor.Global

#endregion

namespace Wyd.Controllers.World
{
    public class WorldController : SingletonController<WorldController>, IPerFrameIncrementalUpdate
    {
        public const float WORLD_HEIGHT = 256f;
        public static readonly float WorldHeightInChunks = Mathf.Floor(WORLD_HEIGHT / ChunkController.Size.y);

        private ChunkController _ChunkControllerPrefab;
        private Dictionary<Vector3, ChunkController> _Chunks;
        private ObjectCache<ChunkController> _ChunkCache;
        private Stack<IEntity> _EntitiesPendingChunkBuilding;
        private Stack<ChunkChangedEventArgs> _ChunksPendingDeactivation;
        private WorldSaveFileProvider _SaveFileProvider;
        private Vector3 _SpawnPoint;

        public CollisionLoaderController CollisionLoaderController;
        public string SeedString;
        public float TicksPerSecond;

        public bool ReadyForGeneration =>
            (_EntitiesPendingChunkBuilding.Count == 0) && (_ChunksPendingDeactivation.Count == 0);

        public int ChunkRegionsActiveCount => _Chunks.Count;
        public int ChunkRegionsCachedCount => _ChunkCache.Size;
        public int ChunkRegionsQueuedForCreation => _EntitiesPendingChunkBuilding.Count;

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

        public event EventHandler<ChunkChangedEventArgs> ChunkMeshChanged;

        #region DEBUG

#if UNITY_EDITOR

        public bool IgnoreInternalFrameLimit;
        public bool StepIntoSelectedChunkStep;
        public GenerationData.GenerationStep SelectedStep;

#endif

        #endregion

        private void Awake()
        {
            AssignSingletonInstance(this);
            SetTickRate();

            _ChunkControllerPrefab = GameController.LoadResource<ChunkController>(@"Prefabs/Chunk");

            _ChunkCache = new ObjectCache<ChunkController>(false, -1,
                (ref ChunkController chunkController) =>
                {
                    if (chunkController != default)
                    {
                        chunkController.Deactivate();
                    }

                    return ref chunkController;
                },
                (ref ChunkController chunkController) =>
                    Destroy(chunkController.gameObject));

            Seed = new WorldSeed(SeedString);
            _Chunks = new Dictionary<Vector3, ChunkController>();
            _EntitiesPendingChunkBuilding = new Stack<IEntity>();
            _ChunksPendingDeactivation = new Stack<ChunkChangedEventArgs>();
            _SaveFileProvider = new WorldSaveFileProvider("world");
            _SaveFileProvider.Initialise().ConfigureAwait(false);
        }

        private void Start()
        {
            _ChunkCache.MaximumSize = OptionsController.Current.MaximumChunkCacheSize;

            TerrainMaterial = GameController.LoadResource<Material>(@"Materials\TerrainMaterial");
            TerrainMaterial.SetTexture(TextureController.MainTexPropertyID,
                TextureController.Current.TerrainTexture);

            EntityController.Current.RegisterWatchForTag(RegisterCollideableEntity, "collider");
            EntityController.Current.RegisterWatchForTag(RegisterLoaderEntity, "loader");

            // todo fix spawn point to set to useful value
            (_SpawnPoint.x, _SpawnPoint.y, _SpawnPoint.z) =
                Mathv.GetIndexAs3D(Seed, new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue));


            if (OptionsController.Current.PreInitializeChunkCache)
            {
                InitialiseChunkCache();
            }
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(10, this);
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(10, this);
        }

        private void OnApplicationQuit()
        {
            WorldSaveFileProvider.ApplicationQuit();
            _SaveFileProvider.Dispose();
        }

        public void FrameUpdate() { }

        public IEnumerable IncrementalFrameUpdate()
        {
            while (_EntitiesPendingChunkBuilding.Count > 0)
            {
                IEntity loader = _EntitiesPendingChunkBuilding.Pop();
                BuildChunksAroundEntity(loader);

                yield return null;
            }

            while (_ChunksPendingDeactivation.Count > 0)
            {
                ChunkChangedEventArgs args = _ChunksPendingDeactivation.Pop();

                // cache position to avoid multiple native dll calls
                Vector3 position = args.ChunkBounds.min;
                CacheChunk(position);
                FlagNeighborsForMeshUpdate(position, args.NeighborDirectionsToUpdate);

                yield return null;
            }
        }

        private void CacheChunk(Vector3 chunkPosition)
        {
            if (!_Chunks.TryGetValue(chunkPosition, out ChunkController chunkController))
            {
                return;
            }

            chunkController.Changed -= OnChunkMeshChanged;
            chunkController.DeactivationCallback -= OnChunkDeactivationCallback;

            _Chunks.Remove(chunkPosition);

            // Chunk is automatically deactivated by ObjectCache
            _ChunkCache.CacheItem(ref chunkController);
        }


        #region TICKS / TIME

        private void SetTickRate()
        {
            if (TicksPerSecond < 1)
            {
                Log.Error(
                    "World tick rate cannot be set to less than 1tick/s. Exiting game.");
                GameController.ApplicationClose();
                return;
            }

            WorldTickRate = TimeSpan.FromSeconds(1d / TicksPerSecond);

            InitialTick = DateTime.Now.Ticks;
        }

        #endregion


        #region CHUNK BUILDING

        public void BuildChunksAroundEntity(IEntity loader)
        {
            int radius = loader.Tags.Contains("player")
                ? OptionsController.Current.RenderDistance + OptionsController.Current.PreLoadChunkDistance
                : 1;

            for (int x = -radius; x < (radius + 1); x++)
            {
                for (int y = 0; y < WorldHeightInChunks; y++)
                {
                    for (int z = -radius; z < (radius + 1); z++)
                    {
                        if (!PerFrameUpdateController.Current.IsInSafeFrameTime())
                        {
                            _EntitiesPendingChunkBuilding.Push(loader);
                            return;
                        }

                        Vector3 position = loader.CurrentChunk
                                           + new Vector3(x, y, z).MultiplyBy(ChunkController.Size);

                        // todo
                        // this will run into the issue of two loaders being within the same render distance
                        // and chunks getting unloaded relative to their loader, but needing to be loaded in
                        // for the other loader sharing render distance
                        if (ChunkExistsAt(position))
                        {
                            continue;
                        }

                        if (!_ChunkCache.TryRetrieve(out ChunkController chunkController))
                        {
                            chunkController = Instantiate(_ChunkControllerPrefab, position, Quaternion.identity,
                                transform);
                        }
                        else
                        {
                            chunkController.Activate(position);
                        }

                        chunkController.Changed += OnChunkMeshChanged;
                        chunkController.DeactivationCallback += OnChunkDeactivationCallback;

                        chunkController.AssignLoader(ref loader);
                        _Chunks.Add(chunkController.Position, chunkController);

                        // ensures that neighbours update their meshes to cull newly out of sight faces
                        FlagNeighborsForMeshUpdate(chunkController.Position, Directions.AllDirectionsVector3);
                    }
                }
            }
        }

        public void FlagNeighborsForMeshUpdate(Vector3 globalChunkPosition, IEnumerable<Vector3> directions)
        {
            foreach (Vector3 normal in directions)
            {
                FlagChunkForUpdateMesh(globalChunkPosition + normal.MultiplyBy(ChunkController.Size));
            }
        }

        public void FlagChunkForUpdateMesh(Vector3 globalChunkPosition)
        {
            if (TryGetChunkAt(globalChunkPosition.RoundBy(ChunkController.Size),
                out ChunkController chunkController))
            {
                chunkController.FlagMeshForUpdate();
            }
        }

        #endregion


        #region EVENTS

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

            loader.ChunkPositionChanged += (sender, vector3) => { _EntitiesPendingChunkBuilding.Push(loader); };
            _EntitiesPendingChunkBuilding.Push(loader);
        }


        private void OnChunkMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            FlagNeighborsForMeshUpdate(args.ChunkBounds.min, args.NeighborDirectionsToUpdate);

            ChunkMeshChanged?.Invoke(sender, args);
        }

        private void OnChunkDeactivationCallback(object sender, ChunkChangedEventArgs args)
        {
            // queue chunk for deactivation so deactivations can be processed in a frame-time sensitive manner
            _ChunksPendingDeactivation.Push(args);
        }

        #endregion


        #region GET / EXISTS

        public bool ChunkExistsAt(Vector3 position) => _Chunks.ContainsKey(position);

        public ChunkController GetChunkAt(Vector3 position)
        {
            bool trySuccess = _Chunks.TryGetValue(position, out ChunkController chunkController);

            return trySuccess ? chunkController : default;
        }

        public bool TryGetChunkAt(Vector3 position, out ChunkController chunkController) =>
            _Chunks.TryGetValue(position, out chunkController);

        public ushort GetBlockAt(Vector3 globalPosition)
        {
            Vector3 chunkPosition = globalPosition.RoundBy(ChunkController.Size);

            ChunkController chunkController = GetChunkAt(chunkPosition);

            if (chunkController == default)
            {
                throw new ArgumentOutOfRangeException(
                    $"Position `{globalPosition}` outside of current loaded radius.");
            }

            return chunkController.BlocksController.GetBlockAt(globalPosition);
        }

        public bool TryGetBlockAt(Vector3 globalPosition, out ushort blockId)
        {
            blockId = default;
            Vector3 chunkPosition = globalPosition.RoundBy(ChunkController.Size);

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && chunkController.BlocksController.TryGetBlockAt(globalPosition, out blockId);
        }

        public bool BlockExistsAt(Vector3 globalPosition)
        {
            Vector3 chunkPosition = globalPosition.RoundBy(ChunkController.Size);

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && chunkController.BlocksController.BlockExistsAt(globalPosition);
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            Vector3 chunkPosition = globalPosition.RoundBy(ChunkController.Size);

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && (chunkController != default)
                   && chunkController.BlocksController.TryPlaceBlockAt(globalPosition, id);
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition)
        {
            Vector3 chunkPosition = globalPosition.RoundBy(ChunkController.Size);

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && (chunkController != default)
                   && chunkController.BlocksController.TryRemoveBlockAt(globalPosition);
        }

        public GenerationData.GenerationStep AggregateNeighborsStep(Vector3 position)
        {
            GenerationData.GenerationStep generationStep = GenerationData.GenerationStep.Complete;

            if (TryGetChunkAt(position + (Vector3.forward * ChunkController.Size.z),
                out ChunkController northChunk))
            {
                generationStep &= northChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Vector3.right * ChunkController.Size.x),
                out ChunkController eastChunk))
            {
                generationStep &= eastChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Vector3.back * ChunkController.Size.z),
                out ChunkController southChunk))
            {
                generationStep &= southChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Vector3.left * ChunkController.Size.x),
                out ChunkController westChunk))
            {
                generationStep &= westChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Vector3.up * ChunkController.Size.x),
                out ChunkController upChunk))
            {
                generationStep &= upChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Vector3.down * ChunkController.Size.x),
                out ChunkController downChunk))
            {
                generationStep &= downChunk.CurrentStep;
            }

            return generationStep;
        }

        #endregion


        #region MISC

        private void InitialiseChunkCache()
        {
            for (int i = 0; i < (OptionsController.Current.MaximumChunkCacheSize / 2); i++)
            {
                ChunkController chunkController =
                    Instantiate(_ChunkControllerPrefab, Vector3.zero, Quaternion.identity, transform);
                chunkController.gameObject.SetActive(false);
                _ChunkCache.CacheItem(ref chunkController);
            }
        }

        #endregion
    }
}
