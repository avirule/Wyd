#region

using System;
using System.Collections;
using System.Collections.Generic;
using Serilog;
using Unity.Mathematics;
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
        public static readonly float WorldHeightInChunks = math.floor(WORLD_HEIGHT / ChunkController.Size.y);

#if UNITY_EDITOR

        private Mesh _Mesh;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
#endif

        private ChunkController _ChunkControllerPrefab;
        private Dictionary<int3, ChunkController> _Chunks;
        private ObjectCache<ChunkController> _ChunkCache;
        private Stack<IEntity> _EntitiesPendingChunkBuilding;
        private Stack<ChunkChangedEventArgs> _ChunksPendingDeactivation;
        private WorldSaveFileProvider _SaveFileProvider;
        private bool _BuildingChunks;

        public CollisionLoaderController CollisionLoaderController;
        public string SeedString;
        public float TicksPerSecond;

        public bool ReadyForGeneration =>
            (_EntitiesPendingChunkBuilding.Count == 0)
            && (_ChunksPendingDeactivation.Count == 0)
            && !_BuildingChunks;

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
        public int3 SpawnPoint { get; private set; }

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

            _ChunkControllerPrefab = SystemController.LoadResource<ChunkController>(@"Prefabs/Chunk");

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
            _Chunks = new Dictionary<int3, ChunkController>();
            _EntitiesPendingChunkBuilding = new Stack<IEntity>();
            _ChunksPendingDeactivation = new Stack<ChunkChangedEventArgs>();
            _SaveFileProvider = new WorldSaveFileProvider("world");
            _SaveFileProvider.Initialise().ConfigureAwait(false);
        }

        private void Start()
        {
            _ChunkCache.MaximumSize = OptionsController.Current.MaximumChunkCacheSize;

            TerrainMaterial = SystemController.LoadResource<Material>(@"Materials\TerrainMaterial");
            TerrainMaterial.SetTexture(TextureController.MainTexPropertyID,
                TextureController.Current.TerrainTexture);

            EntityController.Current.RegisterWatchForTag(RegisterCollideableEntity, "collider");
            EntityController.Current.RegisterWatchForTag(RegisterLoaderEntity, "loader");

            SpawnPoint = WydMath.IndexTo3D(Seed, new int3(int.MaxValue, int.MaxValue, int.MaxValue));


            if (OptionsController.Current.PreInitializeChunkCache)
            {
                InitialiseChunkCache();
            }

            // _Mesh = new Mesh();
            // MeshFilter.sharedMesh = _Mesh;
            // MeshRenderer.material.SetTexture(TextureController.MainTexPropertyID, TextureController.Current.TerrainTexture);
            //
            // List<Vector3> vertices = new List<Vector3>();
            // List<int> triangles = new List<int>();
            // List<Vector3> uvs = new List<Vector3>();
            //
            // Vector3[] verts = BlockFaces.Vertices.North;
            // Vector3[] uvsApply = {
            //     new Vector3(1, 0, 0),
            //     new Vector3(1, 1, 0),
            //     new Vector3(0, 0, 0),
            //     new Vector3(0, 1, 0),
            // };
            //
            // for (int i = 0; i < TextureController.Current.TerrainTexture.depth; i++)
            // {
            //     triangles.AddRange(BlockFaces.Triangles.North.Select(tri => tri + vertices.Count));
            //     vertices.AddRange(verts.Select(vert => vert + Vector3.forward * i));
            //     uvs.AddRange(uvsApply.Select(uv => uv + Vector3.forward * i));
            // }
            //
            // _Mesh.SetVertices(vertices);
            // _Mesh.SetTriangles(triangles, 0);
            // _Mesh.SetUVs(0, uvs);
            // _Mesh.Optimize();
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
                _BuildingChunks = true;

                IEntity loader = _EntitiesPendingChunkBuilding.Pop();
                BuildChunksAroundEntity(loader);

                yield return null;
            }

            while (_ChunksPendingDeactivation.Count > 0)
            {
                ChunkChangedEventArgs args = _ChunksPendingDeactivation.Pop();

                // cache position to avoid multiple native dll calls
                int3 position = WydMath.ToInt(args.ChunkVolume.MinPoint);
                CacheChunk(position);
                FlagNeighborsForMeshUpdate(position, args.NeighborDirectionsToUpdate);

                yield return null;
            }
        }

        private void CacheChunk(int3 chunkPosition)
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
                            _BuildingChunks = false;
                            return;
                        }

                        int3 chunkPosition = loader.ChunkPosition;
                        chunkPosition.y = 0;
                        int3 position = chunkPosition + (new int3(x, y, z) * ChunkController.Size);

                        if (ChunkExistsAt(position))
                        {
                            continue;
                        }

                        if (_ChunkCache.TryRetrieve(out ChunkController chunkController))
                        {
                            chunkController.Activate(position);
                        }
                        else
                        {
                            chunkController = Instantiate(_ChunkControllerPrefab, (float3)position, quaternion.identity,
                                transform);
                        }

                        chunkController.Changed += OnChunkMeshChanged;
                        chunkController.DeactivationCallback += OnChunkDeactivationCallback;

                        chunkController.AssignLoader(ref loader);
                        _Chunks.Add(position, chunkController);

                        Log.Verbose($"Created chunk at {position}.");
                    }
                }
            }

            _BuildingChunks = false;
        }

        private void FlagNeighborsForMeshUpdate(int3 globalChunkPosition, IEnumerable<int3> directions)
        {
            foreach (int3 normal in directions)
            {
                FlagChunkForUpdateMesh(globalChunkPosition + (normal * ChunkController.Size));
            }
        }

        private void FlagChunkForUpdateMesh(int3 globalChunkPosition)
        {
            if (TryGetChunkAt(
                WydMath.ToInt(WydMath.RoundBy(globalChunkPosition, WydMath.ToFloat(ChunkController.Size))),
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

            loader.ChunkPositionChanged += (sender, position) => { _EntitiesPendingChunkBuilding.Push(loader); };
            _EntitiesPendingChunkBuilding.Push(loader);
        }


        private void OnChunkMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            FlagNeighborsForMeshUpdate(WydMath.ToInt(args.ChunkVolume.MinPoint), args.NeighborDirectionsToUpdate);

            ChunkMeshChanged?.Invoke(sender, args);
        }

        private void OnChunkDeactivationCallback(object sender, ChunkChangedEventArgs args)
        {
            // queue chunk for deactivation so deactivations can be processed in a frame-time sensitive manner
            _ChunksPendingDeactivation.Push(args);
        }

        #endregion


        #region GET / EXISTS

        public bool ChunkExistsAt(int3 position) => _Chunks.ContainsKey(position);

        public ChunkController GetChunkAt(int3 position)
        {
            bool trySuccess = _Chunks.TryGetValue(position, out ChunkController chunkController);

            return trySuccess ? chunkController : default;
        }

        public bool TryGetChunkAt(int3 position, out ChunkController chunkController) =>
            _Chunks.TryGetValue(position, out chunkController);

        public ushort GetBlockAt(int3 globalPosition)
        {
            int3 chunkPosition = WydMath.ToInt(WydMath.RoundBy(globalPosition, WydMath.ToFloat(ChunkController.Size)));

            ChunkController chunkController = GetChunkAt(chunkPosition);

            if (chunkController == default)
            {
                throw new ArgumentOutOfRangeException(
                    $"Position `{globalPosition}` outside of current loaded radius.");
            }

            return chunkController.BlocksController.GetBlockAt(globalPosition);
        }

        public bool TryGetBlockAt(int3 globalPosition, out ushort blockId)
        {
            blockId = default;
            int3 chunkPosition = WydMath.ToInt(WydMath.RoundBy(globalPosition, WydMath.ToFloat(ChunkController.Size)));

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && chunkController.BlocksController.TryGetBlockAt(globalPosition, out blockId);
        }

        public bool BlockExistsAt(int3 globalPosition)
        {
            int3 chunkPosition = WydMath.ToInt(WydMath.RoundBy(globalPosition, WydMath.ToFloat(ChunkController.Size)));

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && chunkController.BlocksController.BlockExistsAt(globalPosition);
        }

        public bool TryPlaceBlockAt(int3 globalPosition, ushort id)
        {
            int3 chunkPosition = WydMath.ToInt(WydMath.RoundBy(globalPosition, WydMath.ToFloat(ChunkController.Size)));

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && (chunkController != default)
                   && chunkController.BlocksController.TryPlaceBlockAt(globalPosition, id);
        }

        public bool TryRemoveBlockAt(int3 globalPosition)
        {
            int3 chunkPosition = WydMath.ToInt(WydMath.RoundBy(globalPosition, WydMath.ToFloat(ChunkController.Size)));

            return TryGetChunkAt(chunkPosition, out ChunkController chunkController)
                   && (chunkController != default)
                   && chunkController.BlocksController.TryRemoveBlockAt(globalPosition);
        }

        public GenerationData.GenerationStep AggregateNeighborsStep(int3 position)
        {
            GenerationData.GenerationStep generationStep = GenerationData.GenerationStep.Complete;

            if (TryGetChunkAt(position + (Directions.North * ChunkController.Size.z),
                out ChunkController northChunk))
            {
                generationStep &= northChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Directions.East * ChunkController.Size.x),
                out ChunkController eastChunk))
            {
                generationStep &= eastChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Directions.South * ChunkController.Size.z),
                out ChunkController southChunk))
            {
                generationStep &= southChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Directions.West * ChunkController.Size.x),
                out ChunkController westChunk))
            {
                generationStep &= westChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Directions.Up * ChunkController.Size.x),
                out ChunkController upChunk))
            {
                generationStep &= upChunk.CurrentStep;
            }

            if (TryGetChunkAt(position + (Directions.Down * ChunkController.Size.x),
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
                    Instantiate(_ChunkControllerPrefab, float3.zero, Quaternion.identity, transform);
                chunkController.gameObject.SetActive(false);
                _ChunkCache.CacheItem(ref chunkController);
            }
        }

        #endregion
    }
}
