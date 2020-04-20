#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;
using Unity.Mathematics;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World.Chunk;
using Wyd.Game;
using Wyd.Game.Entities;
using Wyd.Game.World;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Extensions;

// ReSharper disable UnusedAutoPropertyAccessor.Global

#endregion

namespace Wyd.Controllers.World
{
    public class WorldController : SingletonController<WorldController>, IPerFrameIncrementalUpdate
    {
        public const float WORLD_HEIGHT = 256f;

        public static readonly float WorldHeightInChunks = math.floor(WORLD_HEIGHT / ChunkController.SizeCubed.y);


        #region Serialized Members

        public CollisionLoaderController CollisionLoaderController;
        public ChunkController ChunkControllerPrefab;
        public string SeedString;

        #endregion


        #region Instance Members

        private Stopwatch _Stopwatch;
        private ObjectCache<ChunkController> _ChunkCache;
        private Dictionary<float3, ChunkController> _Chunks;
        private ConcurrentStack<float3> _ChunksPendingActivation;
        private ConcurrentStack<float3> _ChunksPendingDeactivation;
        private List<IEntity> _EntityLoaders;
        private object _WorldStateHandle;
        private WorldState _WorldState;

        public WorldState WorldState
        {
            get
            {
                WorldState tmp;

                lock (_WorldStateHandle)
                {
                    tmp = _WorldState;
                }

                return tmp;
            }
            set
            {
                lock (_WorldStateHandle)
                {
                    _WorldState = value;
                }
            }
        }

        public bool ReadyForGeneration =>
            (_ChunksPendingActivation.Count == 0)
            && (_ChunksPendingDeactivation.Count == 0)
            && !WorldState.HasState(WorldState.VerifyingState);

        public int ChunksQueuedCount => _ChunksPendingActivation.Count;
        public int ChunksActiveCount => _Chunks.Count;
        public int ChunksCachedCount => _ChunkCache.Size;

        public double AverageChunkStateVerificationTime =>
            (ChunkStateVerificationTimes != null)
            && (ChunkStateVerificationTimes.Count > 0)
                ? ChunkStateVerificationTimes.Average(time => time.Milliseconds)
                : 0d;

        public FixedConcurrentQueue<TimeSpan> ChunkStateVerificationTimes { get; private set; }
        public WorldSeed Seed { get; private set; }
        public int3 SpawnPoint { get; private set; }

        #endregion


        private void Awake()
        {
            AssignSingletonInstance(this);

            _Stopwatch = new Stopwatch();
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

            _Chunks = new Dictionary<float3, ChunkController>();
            _ChunksPendingActivation = new ConcurrentStack<float3>();
            _ChunksPendingDeactivation = new ConcurrentStack<float3>();
            _EntityLoaders = new List<IEntity>();
            _WorldStateHandle = new object();

            Seed = new WorldSeed(SeedString);
        }

        private void Start()
        {
            SetMaximumChunkCacheSize();
            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.RenderDistance)))
                {
                    SetMaximumChunkCacheSize();
                }
            };

            EntityController.Current.RegisterWatchForTag(RegisterCollideableEntity, "collider");
            EntityController.Current.RegisterWatchForTag(RegisterLoaderEntity, "loader");

            SpawnPoint = WydMath.IndexTo3D(Seed, new int3(int.MaxValue, int.MaxValue, int.MaxValue));

            ChunkStateVerificationTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(10, this);
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(10, this);
        }

        public void FrameUpdate() { }

        public IEnumerable IncrementalFrameUpdate()
        {
            if (WorldState.HasState(WorldState.RequiresStateVerification)
                && !WorldState.HasState(WorldState.VerifyingState))
            {
                WorldState &= ~WorldState.RequiresStateVerification;
                VerifyAllChunkStatesAroundLoaders();
            }

            while (_ChunksPendingActivation.Count > 0)
            {
                if (!_ChunksPendingActivation.TryPop(out float3 origin) || CheckChunkExists(origin))
                {
                    continue;
                }

                if (_ChunkCache.TryRetrieve(out ChunkController chunkController))
                {
                    chunkController.Activate(origin);
                }
                else
                {
                    chunkController = Instantiate(ChunkControllerPrefab, origin,
                        quaternion.identity, transform);
                }

                chunkController.TerrainChanged += OnChunkTerrainChanged;
                chunkController.MeshChanged += OnChunkMeshChanged;
                _Chunks.Add(origin, chunkController);

                Log.Verbose($"Created chunk at {origin}.");

                yield return null;
            }

            while (_ChunksPendingDeactivation.Count > 0)
            {
                if (!_ChunksPendingDeactivation.TryPop(out float3 origin) || !CheckChunkExists(origin))
                {
                    continue;
                }

                CacheChunk(origin);

                yield return null;
            }
        }

        private void CacheChunk(float3 chunkOrigin)
        {
            if (!_Chunks.TryGetValue(chunkOrigin, out ChunkController chunkController))
            {
                return;
            }

            chunkController.TerrainChanged -= OnChunkTerrainChanged;
            chunkController.MeshChanged -= OnChunkMeshChanged;

            _Chunks.Remove(chunkOrigin);

            // Chunk is automatically deactivated by ObjectCache
            _ChunkCache.CacheItem(ref chunkController);

            FlagOriginAndNeighborsForMeshUpdate(chunkOrigin, Directions.AllDirectionAxes);
        }

        private void FlagOriginAndNeighborsForMeshUpdate(float3 origin, IEnumerable<int3> directions)
        {
            FlagChunkForUpdateMesh(origin);

            foreach (float3 normal in directions)
            {
                FlagChunkForUpdateMesh(origin + (normal * ChunkController.SizeCubed));
            }
        }

        private void FlagChunkForUpdateMesh(float3 origin)
        {
            if (TryGetChunk(WydMath.RoundBy(origin, WydMath.ToFloat(ChunkController.SizeCubed)),
                out ChunkController chunkController))
            {
                chunkController.FlagMeshForUpdate();
            }
        }

        public bool CheckNeighborsTerrainComplete(float3 position) =>
            (!TryGetChunk(position + (Directions.North * ChunkController.SizeCubed.z), out ChunkController northChunk)
             || northChunk.ChunkState.HasState(ChunkState.TerrainComplete))
            && (!TryGetChunk(position + (Directions.East * ChunkController.SizeCubed.x), out ChunkController eastChunk)
                || eastChunk.ChunkState.HasState(ChunkState.TerrainComplete))
            && (!TryGetChunk(position + (Directions.South * ChunkController.SizeCubed.z), out ChunkController southChunk)
                || southChunk.ChunkState.HasState(ChunkState.TerrainComplete))
            && (!TryGetChunk(position + (Directions.West * ChunkController.SizeCubed.x), out ChunkController westChunk)
                || westChunk.ChunkState.HasState(ChunkState.TerrainComplete))
            && (!TryGetChunk(position + (Directions.Up * ChunkController.SizeCubed.x), out ChunkController upChunk)
                || upChunk.ChunkState.HasState(ChunkState.TerrainComplete))
            && (!TryGetChunk(position + (Directions.Down * ChunkController.SizeCubed.x), out ChunkController downChunk)
                || downChunk.ChunkState.HasState(ChunkState.TerrainComplete));


        #region State Management

        private void SetMaximumChunkCacheSize()
        {
            if (OptionsController.Current == null)
            {
                return;
            }

            int totalRenderDistance =
                OptionsController.Current.RenderDistance + OptionsController.Current.PreLoadChunkDistance + 1;
            _ChunkCache.MaximumSize = ((totalRenderDistance * 2) - 1) * (int)WorldHeightInChunks;
        }

        private void VerifyAllChunkStatesAroundLoaders()
        {
            _Stopwatch.Restart();

            WorldState |= WorldState.VerifyingState;
            HashSet<float3> chunksRequiringActivation = new HashSet<float3>();
            HashSet<float3> chunksRequiringDeactivation = new HashSet<float3>();

            // get total list of out of bounds chunks
            foreach (IEntity loader in _EntityLoaders)
            {
                // allocate list of chunks requiring deactivation
                foreach ((float3 origin, ChunkController _) in _Chunks)
                {
                    float3 difference = math.abs(origin - loader.ChunkPosition);
                    difference.y = 0; // always load all chunks on y axis

                    if (!IsWithinLoaderRange(difference))
                    {
                        chunksRequiringDeactivation.Add(origin);
                    }
                    else if (chunksRequiringDeactivation.Contains(origin))
                    {
                        chunksRequiringDeactivation.Remove(origin);
                    }
                }

                // todo this should be some setting inside loader
                int renderRadius = OptionsController.Current.RenderDistance
                                   + OptionsController.Current.PreLoadChunkDistance;

                for (int x = -renderRadius; x < (renderRadius + 1); x++)
                for (int z = -renderRadius; z < (renderRadius + 1); z++)
                for (int y = 0; y < WorldHeightInChunks; y++)
                {
                    float3 localOrigin = new float3(x, y, z) * ChunkController.SizeCubed;
                    float3 origin = localOrigin + new float3(loader.ChunkPosition.x, 0, loader.ChunkPosition.z);

                    if (!chunksRequiringActivation.Contains(origin))
                    {
                        chunksRequiringActivation.Add(origin);
                    }
                }
            }

            Log.Debug(
                $"{nameof(WorldController)} state check resulted in '{chunksRequiringActivation.Count}' activations and '{chunksRequiringDeactivation.Count}' deactivations.");

            foreach (float3 origin in chunksRequiringActivation)
            {
                _ChunksPendingActivation.Push(origin);
            }

            foreach (float3 origin in chunksRequiringDeactivation)
            {
                _ChunksPendingDeactivation.Push(origin);
            }

            WorldState &= ~WorldState.VerifyingState;
            ChunkStateVerificationTimes.Enqueue(_Stopwatch.Elapsed);
            _Stopwatch.Reset();
        }

        private static bool IsWithinLoaderRange(float3 difference) =>
            math.all(difference
                     <= (ChunkController.SizeCubed
                         * (OptionsController.Current.RenderDistance
                            + OptionsController.Current.PreLoadChunkDistance)));

        #endregion


        #region Events

        public event ChunkChangedEventHandler ChunkMeshChanged;


        private void OnChunkMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            ChunkMeshChanged?.Invoke(sender, args);
        }

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

            loader.ChunkPositionChanged += (sender, position) => WorldState |= WorldState.RequiresStateVerification;
            WorldState |= WorldState.RequiresStateVerification;
            _EntityLoaders.Add(loader);
        }


        private void OnChunkTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            FlagOriginAndNeighborsForMeshUpdate(args.OriginPoint, args.NeighborDirectionsToUpdate);
        }

        #endregion


        #region Chunk Block Operations

        public bool CheckChunkExists(float3 origin) => _Chunks.ContainsKey(origin);

        public bool TryGetChunk(float3 origin, out ChunkController chunkController) =>
            _Chunks.TryGetValue(origin, out chunkController);

        public bool TryGetBlock(float3 globalPosition, out ushort blockId)
        {
            blockId = BlockController.NullID;
            float3 chunkPosition = WydMath.RoundBy(globalPosition, ChunkController.SizeCubed);

            return TryGetChunk(chunkPosition, out ChunkController chunkController)
                   && chunkController.TryGetBlockAt(globalPosition, out blockId);
        }

        public bool TryPlaceBlock(float3 globalPosition, ushort id)
        {
            float3 chunkPosition = WydMath.RoundBy(globalPosition, ChunkController.SizeCubed);

            return TryGetChunk(chunkPosition, out ChunkController chunkController)
                   && (chunkController != default)
                   && chunkController.TryPlaceBlockAt(globalPosition, id);
        }

        #endregion
    }
}
