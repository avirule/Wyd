#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Collections;
using Wyd.Controllers.App;
using Wyd.Controllers.State;
using Wyd.Entities;
using Wyd.Extensions;
using Wyd.Singletons;
using Wyd.World;
using Wyd.World.Chunks;
using Wyd.World.Chunks.Generation;
using Debug = System.Diagnostics.Debug;

#endregion

namespace Wyd.Controllers.World
{
    public class WorldController : SingletonController<WorldController>, IPerFrameIncrementalUpdate
    {
        public const int WORLD_HEIGHT = 256;
        public const int WORLD_HEIGHT_IN_CHUNKS = WORLD_HEIGHT / GenerationConstants.CHUNK_SIZE;


        #region Instance Members

        private Stopwatch _Stopwatch;
        private ObjectPool<ChunkController> _ChunkControllerPool;
        private Dictionary<float3, ChunkController> _Chunks;
        private Stack<float3> _ChunksPendingActivation;
        private Stack<float3> _ChunksPendingDeactivation;
        private List<IEntity> _EntityLoaders;
        private long _WorldState;

        private HashSet<float3> _ChunksRequiringActivation;
        private HashSet<float3> _ChunksRequiringDeactivation;

        public WorldState WorldState
        {
            get => (WorldState)Interlocked.Read(ref _WorldState);
            set => Interlocked.Exchange(ref _WorldState, (long)value);
        }

        public bool ReadyForGeneration =>
            (_ChunksPendingActivation.Count == 0)
            && (_ChunksPendingDeactivation.Count == 0)
            && !WorldState.HasState(WorldState.VerifyingState);

        public int ChunksQueuedCount => _ChunksPendingActivation.Count;
        public int ChunksActiveCount => _Chunks.Count;
        public int ChunksCachedCount => _ChunkControllerPool.Size;

        public WorldSeed Seed { get; private set; }
        public int3 SpawnPoint { get; private set; }

        #endregion


        #region Serialized Members

        [SerializeField]
        private CollisionLoaderController CollisionLoaderController;

        [SerializeField]
        private ChunkController ChunkControllerPrefab;

        [SerializeField]
        private LineRenderer LineRenderer;

        [SerializeField]
        private string SeedString;

#if UNITY_EDITOR

        [SerializeField]
        private bool RegenerateWorld;

#endif

        #endregion


        private void Awake()
        {
            AssignSingletonInstance(this);

            _ChunkControllerPool = new ObjectPool<ChunkController>();
            _ChunkControllerPool.ItemCulled += (sender, chunkController) => Destroy(chunkController);

            _Stopwatch = new Stopwatch();

            _Chunks = new Dictionary<float3, ChunkController>();
            _ChunksPendingActivation = new Stack<float3>();
            _ChunksPendingDeactivation = new Stack<float3>();
            _EntityLoaders = new List<IEntity>();

            _ChunksRequiringActivation = new HashSet<float3>();
            _ChunksRequiringDeactivation = new HashSet<float3>();

            Seed = new WorldSeed(SeedString);
        }

        private void Start()
        {
            SetMaximumChunkCacheSize();
            Options.Instance.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(Options.Instance.RenderDistance)))
                {
                    SetMaximumChunkCacheSize();
                }
            };

            EntityController.Current.RegisterWatchForTag(RegisterCollideableEntity, "collider");
            EntityController.Current.RegisterWatchForTag(RegisterLoaderEntity, "loader");

            SpawnPoint = WydMath.IndexTo3D(Seed, new int3(int.MaxValue, int.MaxValue, int.MaxValue));

            Singletons.Diagnostics.Instance.RegisterDiagnosticBuffer("WorldStateVerification");
            Singletons.Diagnostics.Instance.RegisterDiagnosticBuffer("ChunkNoiseRetrieval");
            Singletons.Diagnostics.Instance.RegisterDiagnosticBuffer("ChunkBuilding");
            Singletons.Diagnostics.Instance.RegisterDiagnosticBuffer("ChunkDetailing");
            Singletons.Diagnostics.Instance.RegisterDiagnosticBuffer("ChunkPreMeshing");
            Singletons.Diagnostics.Instance.RegisterDiagnosticBuffer("ChunkMeshing");
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(10, this);

            WorldState = WorldState.RequiresStateVerification;
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(10, this);
        }

        public void FrameUpdate()
        {
#if UNITY_EDITOR

            if (RegenerateWorld)
            {
                foreach ((float3 _, ChunkController chunkController) in _Chunks)
                {
                    chunkController.FlagRegenerate();
                }

                RegenerateWorld = false;
            }

#endif
        }

        public IEnumerable IncrementalFrameUpdate()
        {
            if (WorldState.HasState(WorldState.RequiresStateVerification)
                && !WorldState.HasState(WorldState.VerifyingState))
            {
                WorldState &= ~WorldState.RequiresStateVerification;
                VerifyAllChunkStatesAroundLoaders();
            }
            else if (WorldState.HasState(WorldState.VerifyingState))
            {
                yield break;
            }

            while (_ChunksPendingActivation.Count > 0)
            {
                // yield at the start since these operations are very time consuming
                // this allows the PerFrameIncrementalUpdater to cancel the operation early
                yield return null;

                float3 origin = _ChunksPendingActivation.Pop();

                Debug.Assert(!CheckChunkExists(origin));

                if (_ChunkControllerPool.TryTake(out ChunkController chunkController))
                {
                    chunkController.Activate(origin);
                }
                else
                {
                    chunkController = Instantiate(ChunkControllerPrefab, origin, quaternion.identity, transform);
                }

                chunkController.TerrainChanged += OnChunkShapeChanged;
                chunkController.BlocksChanged += OnChunkShapeChanged;
                chunkController.MeshChanged += OnChunkMeshChanged;
                _Chunks.Add(origin, chunkController);

                Log.Verbose($"({nameof(WorldController)}) Chunk created: {origin}.");
            }

            while (_ChunksPendingDeactivation.Count > 0)
            {
                // yield at the start since these operations are very time consuming
                // this allows the PerFrameIncrementalUpdater to cancel the operation early
                yield return null;

                float3 origin = _ChunksPendingDeactivation.Pop();

                Debug.Assert(CheckChunkExists(origin));

                CacheChunk(origin);
            }
        }

        private void CacheChunk(float3 origin)
        {
            if (!_Chunks.TryGetValue(origin, out ChunkController chunkController))
            {
                return;
            }

            chunkController.TerrainChanged -= OnChunkShapeChanged;
            chunkController.BlocksChanged -= OnChunkShapeChanged;
            chunkController.MeshChanged -= OnChunkMeshChanged;

            FlagNeighborsForMeshUpdate(chunkController.OriginPoint);
            _Chunks.Remove(origin);

            chunkController.Deactivate();

            if (_ChunkControllerPool.TryAdd(chunkController))
            {
                Log.Verbose($"({nameof(WorldController)}) Chunk cached: {origin}.");
            }
            else
            {
                Destroy(chunkController.gameObject);
                Log.Verbose($"({nameof(WorldController)}) Chunk destroyed: {origin}.");
            }
        }

        public IEnumerable<ushort> GetNeighboringBlocks(int3 globalPosition) =>
            Directions.AllDirectionNormals.Select(normal => GetBlock(globalPosition + normal));

        public IEnumerable<ChunkController> GetNeighboringChunks(float3 origin)
        {
            foreach (float3 normal in Directions.AllDirectionNormals)
            {
                if (TryGetChunk(origin + (normal * GenerationConstants.CHUNK_SIZE), out ChunkController chunkController))
                {
                    yield return chunkController;
                }
            }
        }

        public IEnumerable<ChunkController> GetNeighboringChunks(float3 origin, IEnumerable<float3> normals)
        {
            foreach (float3 normal in normals)
            {
                if (TryGetChunk(origin + (normal * GenerationConstants.CHUNK_SIZE), out ChunkController chunkController))
                {
                    yield return chunkController;
                }
            }
        }

        public IEnumerable<(int3, ChunkController)> GetNeighboringChunksWithNormal(float3 origin)
        {
            foreach (int3 normal in Directions.AllDirectionNormals)
            {
                if (TryGetChunk(origin + (normal * GenerationConstants.CHUNK_SIZE), out ChunkController chunkController))
                {
                    yield return (normal, chunkController);
                }
            }
        }

        private void FlagNeighborsForMeshUpdate(float3 chunkOrigin)
        {
            foreach (ChunkController neighborChunk in GetNeighboringChunks(chunkOrigin))
            {
                neighborChunk.FlagMeshForUpdate();
            }
        }


        #region State Management

        private void SetMaximumChunkCacheSize()
        {
            int totalRenderDistance = Options.Instance.RenderDistance + /* OptionsController.Current.PreLoadChunkDistance + */ 1;
            _ChunkControllerPool.SetMaximumSize(((totalRenderDistance * 2) - 1) * WORLD_HEIGHT_IN_CHUNKS);
        }

        private void VerifyAllChunkStatesAroundLoaders()
        {
            _Stopwatch.Restart();

            WorldState |= WorldState.VerifyingState;

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
                        _ChunksRequiringDeactivation.Add(origin);
                    }
                    else
                    {
                        _ChunksRequiringDeactivation.Remove(origin);
                    }
                }

                // todo this should be some setting inside loader
                int renderRadius = Options.Instance.RenderDistance;

                for (int x = -renderRadius; x < (renderRadius + 1); x++)
                for (int z = -renderRadius; z < (renderRadius + 1); z++)
                for (int y = 0; y < WORLD_HEIGHT_IN_CHUNKS; y++)
                {
                    float3 localOrigin = new float3(x, y, z) * GenerationConstants.CHUNK_SIZE;
                    float3 globalOrigin = localOrigin + new float3(loader.ChunkPosition.x, 0, loader.ChunkPosition.z);

                    _ChunksRequiringActivation.Add(globalOrigin);
                }
            }

            Log.Debug(
                $"({nameof(WorldController)}) State verification: {_ChunksRequiringActivation.Count} activations, {_ChunksRequiringDeactivation.Count} deactivations.");

            foreach (float3 origin in _ChunksRequiringActivation.Where(origin => !CheckChunkExists(origin)))
            {
                _ChunksPendingActivation.Push(origin);
            }

            foreach (float3 origin in _ChunksRequiringDeactivation)
            {
                _ChunksPendingDeactivation.Push(origin);
            }

            _ChunksRequiringActivation.Clear();
            _ChunksRequiringDeactivation.Clear();

            WorldState &= ~WorldState.VerifyingState;

            Singletons.Diagnostics.Instance["WorldStateVerification"].Enqueue(_Stopwatch.Elapsed);

            _Stopwatch.Reset();
        }

        private static bool IsWithinLoaderRange(float3 difference) =>
            math.all(difference <= (GenerationConstants.CHUNK_SIZE * Options.Instance.RenderDistance));

        #endregion


        #region Chunk Block Operations

        public bool CheckChunkExists(float3 origin) => _Chunks.ContainsKey(origin);

        public bool TryGetChunk(float3 origin, out ChunkController chunkController) =>
            _Chunks.TryGetValue(origin, out chunkController);

        public ushort GetBlock(int3 globalPosition)
        {
            if (!TryGetChunk(WydMath.RoundBy(globalPosition, GenerationConstants.CHUNK_SIZE), out ChunkController chunkController))
            {
                throw new ArgumentException("No chunk exists around given coordinate.", nameof(globalPosition));
            }

            return chunkController.GetBlock(globalPosition);
        }

        public bool TryGetBlock(int3 globalPosition, out ushort blockId)
        {
            blockId = BlockController.NullID;
            return TryGetChunk(WydMath.RoundBy(globalPosition, GenerationConstants.CHUNK_SIZE), out ChunkController chunkController)
                   && chunkController.TryGetBlock(math.abs(chunkController.OriginPoint - globalPosition), out blockId);
        }

        public void PlaceBlock(int3 globalPosition, ushort id)
        {
            if (!TryGetChunk(WydMath.RoundBy(globalPosition, GenerationConstants.CHUNK_SIZE), out ChunkController chunkController))
            {
                throw new ArgumentException("No chunk exists around given coordinate.", nameof(globalPosition));
            }

            chunkController.PlaceBlock(globalPosition, id);
        }

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


        private void OnChunkShapeChanged(object sender, ChunkChangedEventArgs args)
        {
            foreach (ChunkController chunkController in GetNeighboringChunks(args.OriginPoint,
                args.NeighborDirectionsToUpdate))
            {
                chunkController.FlagMeshForUpdate();
            }
        }

        #endregion
    }
}
