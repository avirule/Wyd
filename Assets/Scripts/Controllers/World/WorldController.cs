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
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Entities;
using Wyd.Extensions;
using Wyd.World;
using Wyd.World.Chunks;
using Wyd.World.Chunks.Generation;

// ReSharper disable UnusedAutoPropertyAccessor.Global

#endregion

namespace Wyd.Controllers.World
{
    public class WorldController : SingletonController<WorldController>, IPerFrameIncrementalUpdate
    {
        public const int WORLD_HEIGHT = 256;
        public const int WORLD_HEIGHT_IN_CHUNKS = WORLD_HEIGHT / GenerationConstants.CHUNK_SIZE;


        #region Instance Members

        private Stopwatch _Stopwatch;
        private ObjectPool<ChunkController> _ChunkPool;
        private Dictionary<float3, ChunkController> _Chunks;
        private Stack<float3> _ChunksPendingActivation;
        private Stack<float3> _ChunksPendingDeactivation;
        private List<IEntity> _EntityLoaders;
        private long _WorldState;

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
        public int ChunksCachedCount => _ChunkPool.Size;

        public double AverageChunkStateVerificationTime =>
            (ChunkStateVerificationTimes != null)
            && (ChunkStateVerificationTimes.Count > 0)
                ? ChunkStateVerificationTimes.Average(time => time.Milliseconds)
                : 0d;

        public FixedConcurrentQueue<TimeSpan> ChunkStateVerificationTimes { get; private set; }
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
        public float InterimGenerationValue;

        [SerializeField]
        private bool RegenerateWorld;

#endif

        #endregion


        private void Awake()
        {
            AssignSingletonInstance(this);

            _Stopwatch = new Stopwatch();
            _ChunkPool = new ObjectPool<ChunkController>();

            _Chunks = new Dictionary<float3, ChunkController>();
            _ChunksPendingActivation = new Stack<float3>();
            _ChunksPendingDeactivation = new Stack<float3>();
            _EntityLoaders = new List<IEntity>();

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
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.DiagnosticBufferLength);
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(10, this);
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
                yield return null; // yield at the start since these operations are very time consuming

                float3 origin = _ChunksPendingActivation.Pop();

                if (CheckChunkExists(origin))
                {
                    continue;
                }

                if (_ChunkPool.TryRetrieve(out ChunkController chunkController))
                {
                    chunkController.Activate(origin);
                }
                else
                {
                    chunkController = Instantiate(ChunkControllerPrefab, origin,
                        quaternion.identity, transform);
                }

                chunkController.TerrainChanged += OnChunkShapeChanged;
                chunkController.BlocksChanged += OnChunkShapeChanged;
                chunkController.MeshChanged += OnChunkMeshChanged;
                _Chunks.Add(origin, chunkController);

                Log.Verbose($"Created chunk at {origin}.");
            }

            while (_ChunksPendingDeactivation.Count > 0)
            {
                yield return null; // yield at the start since these operations are very time consuming

                float3 origin = _ChunksPendingDeactivation.Pop();

                if (!CheckChunkExists(origin))
                {
                    continue;
                }

                CacheChunk(origin);
            }
        }

        private void CacheChunk(float3 chunkOrigin)
        {
            if (!_Chunks.TryGetValue(chunkOrigin, out ChunkController chunkController))
            {
                return;
            }

            chunkController.TerrainChanged -= OnChunkShapeChanged;
            chunkController.BlocksChanged += OnChunkShapeChanged;
            chunkController.MeshChanged -= OnChunkMeshChanged;

            FlagNeighborsForMeshUpdate(chunkController.OriginPoint);
            _Chunks.Remove(chunkOrigin);

            chunkController.Deactivate();

            // Chunk is automatically deactivated by ObjectPool
            // additionally, neighbors are flagged for update by ObjectPool
            if (!_ChunkPool.TryAdd(chunkController))
            {
                Destroy(chunkController.gameObject);
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

        public IEnumerable<(Direction, ChunkController)> GetNeighboringChunksWithDirection(float3 origin)
        {
            foreach (int3 normal in Directions.AllDirectionNormals)
            {
                if (TryGetChunk(origin + (normal * GenerationConstants.CHUNK_SIZE), out ChunkController chunkController))
                {
                    yield return (Directions.NormalToDirection(normal), chunkController);
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

        public IEnumerable<ChunkController> GetVerticalSlice(float2 origin)
        {
            if (((origin.x % GenerationConstants.CHUNK_SIZE) > 0f) || ((origin.y % GenerationConstants.CHUNK_SIZE) > 0f))
            {
                throw new ArgumentException("Given coordinates must be chunk-aligned.", nameof(origin));
            }

            for (int y = 0; y < WORLD_HEIGHT_IN_CHUNKS; y++)
            {
                float3 chunkOrigin = new float3(origin.x, y * GenerationConstants.CHUNK_SIZE, origin.y);

                if (TryGetChunk(chunkOrigin, out ChunkController chunkController))
                {
                    yield return chunkController;
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
            if (OptionsController.Current == null)
            {
                return;
            }

            int totalRenderDistance =
                OptionsController.Current.RenderDistance + /*OptionsController.Current.PreLoadChunkDistance*/ +1;
            _ChunkPool.SetMaximumSize(((totalRenderDistance * 2) - 1) * WORLD_HEIGHT_IN_CHUNKS);
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
                    /*+ OptionsController.Current.PreLoadChunkDistance*/;

                for (int x = -renderRadius; x < (renderRadius + 1); x++)
                for (int z = -renderRadius; z < (renderRadius + 1); z++)
                for (int y = 0; y < WORLD_HEIGHT_IN_CHUNKS; y++)
                {
                    float3 localOrigin = new float3(x, y, z) * GenerationConstants.CHUNK_SIZE;
                    float3 globalOrigin = localOrigin + new float3(loader.ChunkPosition.x, 0, loader.ChunkPosition.z);

                    if (!chunksRequiringActivation.Contains(globalOrigin))
                    {
                        chunksRequiringActivation.Add(globalOrigin);
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
            math.all(difference <= (GenerationConstants.CHUNK_SIZE * OptionsController.Current.RenderDistance));

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
