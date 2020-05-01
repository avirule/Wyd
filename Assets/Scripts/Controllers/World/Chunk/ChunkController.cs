#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Compression.LZ4;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game.World.Blocks;
using Wyd.Game.World.Chunks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Extensions;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public enum ChunkState
    {
        Unbuilt,
        BuildingDispatched,
        Undetailed,
        DetailingDispatched,
        Mesh,
        AwaitingMesh,
        Meshed
    }

    public class ChunkController : ActivationStateChunkController, IPerFrameIncrementalUpdate
    {
        private const float _FREQUENCY = 0.0075f;
        private const float _PERSISTENCE = 0.6f;

        public const int SIZE = 32;
        public const int SIZE_SQUARED = SIZE * SIZE;
        public const int SIZE_CUBED = SIZE * SIZE * SIZE;

        private static readonly ObjectCache<BlockAction> _blockActionsCache = new ObjectCache<BlockAction>(false, 1024);


        #region NoiseShader

        private static ComputeShader _NoiseShader;

        private static void InitializeNoiseShader()
        {
            _NoiseShader = Resources.Load<ComputeShader>(@"Graphics\Shaders\OpenSimplex");
            _NoiseShader.SetInt("_HeightmapSeed", WorldController.Current.Seed);
            _NoiseShader.SetInt("_CaveNoiseSeedA", WorldController.Current.Seed ^ 2);
            _NoiseShader.SetInt("_CaveNoiseSeedB", WorldController.Current.Seed ^ 3);
            _NoiseShader.SetFloat("_WorldHeight", WorldController.WORLD_HEIGHT);
            _NoiseShader.SetVector("_MaximumSize", new float4(SIZE));
        }

        #endregion


        #region Instance Members

        private CancellationTokenSource _CancellationTokenSource;
        private ChunkBlockData _BlockData;
        private ConcurrentQueue<BlockAction> _BlockActions;
        private List<ChunkController> _Neighbors;

        private Mesh _Mesh;
        private bool _CacheNeighbors;
        private long _BlockActionsCount;
        private long _State;
        private long _Active;
        private long _UpdateMesh;

        private object _MeshJobIdentity;
        private ChunkMeshingJob _FinishedMeshingJob;

        private bool UpdateMesh
        {
            get => Convert.ToBoolean(Interlocked.Read(ref _UpdateMesh));
            set => Interlocked.Exchange(ref _UpdateMesh, Convert.ToInt64(value));
        }

        public bool Active
        {
            get => Convert.ToBoolean(Interlocked.Read(ref _Active));
            set => Interlocked.Exchange(ref _Active, Convert.ToInt64(value));
        }

        public ChunkState State
        {
            get => (ChunkState)Interlocked.Read(ref _State);
            private set
            {
                Interlocked.Exchange(ref _State, (long)value);

#if UNITY_EDITOR

                InternalState = State;

#endif
            }
        }

        public INodeCollection<ushort> Blocks => _BlockData.Blocks;

        #endregion


        #region Serialized Members

        [SerializeField]
        private ChunkMeshController MeshController;

#if UNITY_EDITOR

        [SerializeField]
        [ReadOnlyInspectorField]
        private Vector3 MinimumPoint;

        [SerializeField]
        [ReadOnlyInspectorField]
        private Vector3 MaximumPoint;

        [SerializeField]
        [ReadOnlyInspectorField]
        private ChunkState InternalState;

        [SerializeField]
        private bool Regenerate;
#endif

        #endregion


        #region Unity Built-ins

        protected override void Awake()
        {
            base.Awake();

            if (_NoiseShader == null)
            {
                InitializeNoiseShader();
            }

            _Neighbors = new List<ChunkController>();
            _BlockActions = new ConcurrentQueue<BlockAction>();
            _Mesh = new Mesh();
            _BlockData = new ChunkBlockData();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(20, this);

            _CancellationTokenSource = new CancellationTokenSource();
            Active = gameObject.activeSelf;
            _CacheNeighbors = true;

            BlocksChanged += FlagUpdateMeshCallback;
            TerrainChanged += FlagUpdateMeshCallback;

#if UNITY_EDITOR

            MinimumPoint = WydMath.ToFloat(OriginPoint);
            MaximumPoint = WydMath.ToFloat(OriginPoint + SIZE);

#endif
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(20, this);

            if (_Mesh != null)
            {
                _Mesh.Clear();
            }

            _CancellationTokenSource.Cancel();
            _Neighbors.Clear();
            _BlockData.Deallocate();
            _BlockActions = new ConcurrentQueue<BlockAction>();

            _MeshJobIdentity = null;
            _FinishedMeshingJob = null;

            State = ChunkState.Unbuilt;
            UpdateMesh = false;
            BlocksChanged = TerrainChanged = MeshChanged = null;
            Active = gameObject.activeSelf;
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);

            _CancellationTokenSource?.Cancel();
            _Neighbors = null;
            _BlockData = null;
            _BlockActions = null;

            State = 0;
            UpdateMesh = false;
            BlocksChanged = TerrainChanged = MeshChanged = null;
            Active = false;
        }

        #endregion


        public void FrameUpdate()
        {
#if UNITY_EDITOR

            if (Regenerate)
            {
                State = ChunkState.Unbuilt;
                _FinishedMeshingJob = null;
                Regenerate = false;
            }

#endif

            if (_CacheNeighbors)
            {
                _Neighbors.InsertRange(0, WorldController.Current.GetNeighboringChunks(OriginPoint));

                State = ChunkState.Unbuilt;

                _CacheNeighbors = false;
            }

            if (((State == ChunkState.Meshed) && !UpdateMesh) || !WorldController.Current.ReadyForGeneration)
            {
                return;
            }
            else if ((State > ChunkState.Unbuilt) && (State < ChunkState.Mesh))
            {
                if (_Neighbors.Any(chunkController => chunkController.State < State))
                {
                    return;
                }
            }

            switch (State)
            {
                case ChunkState.Unbuilt:
                    BeginTerrainBuilding();

                    State = State.Next();
                    break;
                // case ChunkState.Undetailed:
                //     TerrainController.BeginTerrainDetailing(_CancellationTokenSource.Token, OnTerrainDetailingFinished, _Blocks, out _TerrainJobIdentity);
                //
                //     State = State.Next();
                //     break;
                // case ChunkState.DetailingDispatched:
                //     if (_FinishedTerrainJob != null)
                //     {
                //         _FinishedTerrainJob.GetGeneratedBlockData(out _Blocks);
                //         _FinishedTerrainJob = null;
                //         State = State.Next();
                //         OnLocalTerrainChanged(this, new ChunkChangedEventArgs(OriginPoint, Directions.AllDirectionNormals.Select(WydMath.ToFloat)));
                //     }
                //
                //     State = State.Next();
                //
                //     break;
                case ChunkState.Mesh:
                    if (Blocks.IsUniform
                        && ((Blocks.Value == BlockController.AirID)
                            || _Neighbors.All(chunkController => (chunkController.Blocks != null)
                                                                 && chunkController.Blocks.IsUniform)))
                    {
                        State = ChunkState.Meshed;
                    }
                    else
                    {
                        MeshController.BeginGeneratingMesh(_CancellationTokenSource.Token, OnMeshingFinished, Blocks, out _MeshJobIdentity);
                        State = State.Next();
                    }

                    UpdateMesh = false;

                    break;
                case ChunkState.AwaitingMesh:
                    if (_FinishedMeshingJob != null)
                    {
                        MeshController.ApplyMesh(_FinishedMeshingJob);
                        _FinishedMeshingJob = null;
                        State = State.Next();
                        OnMeshChanged(this, new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<float3>()));
                    }

                    break;
                case ChunkState.Meshed:
                    if (UpdateMesh && _BlockActions.IsEmpty)
                    {
                        State = ChunkState.Mesh;
                    }

                    break;
                case ChunkState.Undetailed:
                case ChunkState.DetailingDispatched:
                    State = State.Next();
                    break;
                case ChunkState.BuildingDispatched:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IEnumerable IncrementalFrameUpdate()
        {
            if ((State < ChunkState.Mesh) || (Interlocked.Read(ref _BlockActionsCount) == 0))
            {
                yield break;
            }

            while (_BlockActions.TryDequeue(out BlockAction blockAction))
            {
                ProcessBlockAction(blockAction);

                _blockActionsCache.CacheItem(ref blockAction);

                Interlocked.Decrement(ref _BlockActionsCount);

                yield return null;
            }
        }

        public void FlagMeshForUpdate()
        {
            if (!UpdateMesh)
            {
                UpdateMesh = true;
            }
        }

#if UNITY_EDITOR

        public void FlagRegenerate()
        {
            Regenerate = true;
        }

#endif

        public void Compress()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            byte[] bytes = WydMath.ObjectToByteArray(_BlockData.Blocks);
            byte[] target = new byte[bytes.Length];

            Log.Information($"Serialized in {stopwatch.ElapsedMilliseconds}ms for {bytes.Length / 1000}kb");

            int bytesUsed = LZ4Codec.Encode(bytes, 0, bytes.Length, target, 0, target.Length);

            byte[] final = new byte[bytesUsed];
            Array.Copy(target, 0, final, 0, final.Length);

            stopwatch.Stop();

            Log.Information($"{stopwatch.ElapsedMilliseconds}ms from {bytes.Length / 1000}kb to {final.Length / 1000}kb");
        }


        #region De/Activation

        public void Activate(float3 position)
        {
            _SelfTransform.position = position;

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        #endregion


        #region Generation

        private void BeginTerrainBuilding()
        {
            ComputeBuffer heightmapBuffer = new ComputeBuffer(SIZE_SQUARED, 4);
            ComputeBuffer caveNoiseBuffer = new ComputeBuffer(SIZE_CUBED, 4);

            _NoiseShader.SetVector("_Offset", new float4(OriginPoint.xyzz));
            _NoiseShader.SetFloat("_Frequency", _FREQUENCY);
            _NoiseShader.SetFloat("_Persistence", _PERSISTENCE);
            _NoiseShader.SetFloat("_SurfaceHeight", WorldController.WORLD_HEIGHT / 2f);

            int heightmapKernel = _NoiseShader.FindKernel("Heightmap2D");
            _NoiseShader.SetBuffer(heightmapKernel, "HeightmapResult", heightmapBuffer);

            int caveNoiseKernel = _NoiseShader.FindKernel("CaveNoise3D");
            _NoiseShader.SetBuffer(caveNoiseKernel, "CaveNoiseResult", caveNoiseBuffer);

            _NoiseShader.Dispatch(heightmapKernel, 1024, 1, 1);
            _NoiseShader.Dispatch(caveNoiseKernel, 1024, 1, 1);

            ChunkTerrainJob asyncJob = new ChunkTerrainBuilderJob(_CancellationTokenSource.Token, OriginPoint, _FREQUENCY, _PERSISTENCE,
                OptionsController.Current.GPUAcceleration ? heightmapBuffer : null,
                OptionsController.Current.GPUAcceleration ? caveNoiseBuffer : null);

            Task OnTerrainBuildingFinished(object sender, AsyncJobEventArgs args)
            {
                asyncJob.WorkFinished -= OnTerrainBuildingFinished;

                if (Active)
                {
                    MainThreadActionsController.Current.QueueAction(new MainThreadAction(null, () =>
                    {
                        heightmapBuffer?.Release();
                        caveNoiseBuffer?.Release();

                        return true;
                    }));

                    asyncJob.GetGeneratedBlockData(out INodeCollection<ushort> blocks);
                    _BlockData.SetBlockData(ref blocks);

                    State = ChunkState.Undetailed;
                }

                return Task.CompletedTask;
            }

            asyncJob.WorkFinished += OnTerrainBuildingFinished;

            AsyncJobScheduler.QueueAsyncJob(asyncJob);
        }

        public void BeginTerrainDetailing(CancellationToken cancellationToken, AsyncJobEventHandler callback, INodeCollection<ushort> blocks,
            out object jobIdentity)
        {
            ChunkTerrainDetailerJob asyncJob = new ChunkTerrainDetailerJob(cancellationToken, OriginPoint, blocks);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            jobIdentity = asyncJob.Identity;

            AsyncJobScheduler.QueueAsyncJob(asyncJob);
        }

        #endregion


        #region Block Actions

        public ushort GetBlock(float3 globalPosition) =>
            Blocks?.GetPoint(math.abs(globalPosition - OriginPoint)) ?? BlockController.NullID;

        public bool TryGetBlock(float3 localPosition, out ushort blockId)
        {
            blockId = BlockController.NullID;

            if (math.any(localPosition < 0f) || math.any(localPosition >= SIZE) || (_BlockData.Blocks == null))
            {
                return false;
            }

            blockId = Blocks.GetPoint(localPosition);
            return true;
        }

        public void PlaceBlock(float3 globalPosition, ushort newBlockId) =>
            AllocateBlockAction(math.abs(globalPosition - OriginPoint), newBlockId);

        private void AllocateBlockAction(float3 localPosition, ushort id)
        {
            if (_blockActionsCache.TryRetrieve(out BlockAction blockAction))
            {
                blockAction.SetData(localPosition, id);
            }
            else
            {
                blockAction = new BlockAction(localPosition, id);
            }

            _BlockActions.Enqueue(blockAction);

            Interlocked.Increment(ref _BlockActionsCount);
        }

        private void ProcessBlockAction(BlockAction blockAction)
        {
            Blocks.SetPoint(blockAction.LocalPosition, blockAction.Id);

            OnBlocksChanged(this, new ChunkChangedEventArgs(OriginPoint, GetActionAdjacentNeighbors(blockAction.LocalPosition)));
        }

        private IEnumerable<float3> GetActionAdjacentNeighbors(float3 globalPosition)
        {
            float3 localPosition = math.abs(globalPosition - OriginPoint);

            List<float3> normals = new List<float3>();
            normals.AddRange(WydMath.ToComponents(math.select(float3.zero, new float3(1f), localPosition == (SIZE - 1f))));
            normals.AddRange(WydMath.ToComponents(math.select(float3.zero, new float3(1f), localPosition == 0f)));

            return normals;
        }

        #endregion


        #region Events

        public event ChunkChangedEventHandler BlocksChanged;
        public event ChunkChangedEventHandler TerrainChanged;
        public event ChunkChangedEventHandler MeshChanged;


        private void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

        private void OnLocalTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            TerrainChanged?.Invoke(sender, args);
        }

        private void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }


        private Task OnTerrainBuildingFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnTerrainBuildingFinished;

            if (!Active)
            {
                return Task.CompletedTask;
            }


            return Task.CompletedTask;
        }

        private Task OnMeshingFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnMeshingFinished;

            if (!args.AsyncJob.Identity.Equals(_MeshJobIdentity))
            {
                ((ChunkMeshingJob)args.AsyncJob).CacheMesher();
                return Task.CompletedTask;
            }

            _FinishedMeshingJob = (ChunkMeshingJob)args.AsyncJob;
            return Task.CompletedTask;
        }

        private void FlagUpdateMeshCallback(object sender, ChunkChangedEventArgs args)
        {
            FlagMeshForUpdate();
        }

        #endregion
    }
}
