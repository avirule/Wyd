#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ConcurrentAsyncScheduler;
using K4os.Compression.LZ4;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Collections;
using Wyd.Controllers.App;
using Wyd.Controllers.State;
using Wyd.Diagnostics;
using Wyd.Extensions;
using Wyd.Singletons;
using Wyd.World.Blocks;
using Wyd.World.Chunks;
using Wyd.World.Chunks.Generation;
using Debug = System.Diagnostics.Debug;

#endregion

namespace Wyd.Controllers.World
{
    public enum ChunkState
    {
        Unbuilt,
        AwaitingBuilding,
        Unmeshed,
        AwaitingMeshing,
        Meshed
    }

    public class ChunkController : MonoBehaviour, IPerFrameIncrementalUpdate
    {
        private static readonly ObjectPool<BlockAction> _BlockActionsPool = new ObjectPool<BlockAction>(1024);
        private static readonly ObjectPool<ChunkBuildingJob> _BuildingJobs = new ObjectPool<ChunkBuildingJob>();
        private static readonly ObjectPool<ChunkMeshingJob> _MeshingJobs = new ObjectPool<ChunkMeshingJob>();


        public void FrameUpdate()
        {
#if UNITY_EDITOR

            if (Regenerate)
            {
                State = ChunkState.Unbuilt;
                Regenerate = false;
            }

#endif

            if (_BuildingJobs.MaximumSize != WorldController.WorldExpansionEdgeSize)
            {
                _BuildingJobs.SetMaximumSize(WorldController.WorldExpansionEdgeSize);
            }

            if (_MeshingJobs.MaximumSize != WorldController.WorldExpansionEdgeSize)
            {
                _MeshingJobs.SetMaximumSize(WorldController.WorldExpansionEdgeSize);
            }

            if (_GenerateNeighbors)
            {
                _Neighbors.InsertRange(0, WorldController.Current.GetNeighboringChunks(OriginPoint));

                State = ChunkState.Unbuilt;

                _GenerateNeighbors = false;
            }

            if (((State == ChunkState.Meshed) && !UpdateMesh) || !WorldController.Current.ReadyForGeneration)
            {
                return;
            }
            else if ((State > ChunkState.Unbuilt) && (State < ChunkState.Unmeshed))
            {
                if (_Neighbors.Any(chunkController => chunkController.State < State))
                {
                    return;
                }
            }

            switch (State)
            {
                case ChunkState.Unbuilt:
                    BeginBuilding();

                    State = State.Next();
                    break;
                case ChunkState.AwaitingBuilding:
                    break;
                case ChunkState.Unmeshed:
                    if (Blocks.IsUniform
                        && ((Blocks.Value == BlockController.AirID)
                            || _Neighbors.All(chunkController => (chunkController.Blocks != null)
                                                                 && chunkController.Blocks.IsUniform)))
                    {
                        State = ChunkState.Meshed;
                    }
                    else
                    {
                        BeginMeshing();
                        State = State.Next();
                    }

                    UpdateMesh = false;

                    break;
                case ChunkState.AwaitingMeshing:
                    break;
                case ChunkState.Meshed:
                    if (UpdateMesh && _BlockActions.IsEmpty)
                    {
                        State = ChunkState.Unmeshed;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IEnumerable IncrementalFrameUpdate()
        {
            if ((State < ChunkState.Unmeshed) || (Interlocked.Read(ref _BlockActionsCount) == 0))
            {
                yield break;
            }

            while (_BlockActions.TryDequeue(out BlockAction blockAction))
            {
                ProcessBlockAction(blockAction);

                _BlockActionsPool.TryAdd(blockAction);

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

            byte[] bytes = WydMath.ObjectToByteArray(Blocks);
            byte[] target = new byte[bytes.Length];

            Log.Information($"Serialized in {stopwatch.ElapsedMilliseconds}ms for {bytes.Length / 1000}kb");

            int bytesUsed = LZ4Codec.Encode(bytes, 0, bytes.Length, target, 0, target.Length);

            byte[] final = new byte[bytesUsed];
            Array.Copy(target, 0, final, 0, final.Length);

            stopwatch.Stop();

            Log.Information($"{stopwatch.ElapsedMilliseconds}ms from {bytes.Length / 1000}kb to {final.Length / 1000}kb");
        }


        #region NoiseShader

        private static ComputeShader _NoiseShader;

        private static void InitializeNoiseShader()
        {
            _NoiseShader = Resources.Load<ComputeShader>(@"Graphics\Shaders\OpenSimplex");
            _NoiseShader.SetInt("_HeightmapSeed", WorldController.Current.Seed);
            _NoiseShader.SetInt("_CaveNoiseSeedA", WorldController.Current.Seed ^ 2);
            _NoiseShader.SetInt("_CaveNoiseSeedB", WorldController.Current.Seed ^ 3);
            _NoiseShader.SetFloat("_WorldHeight", WorldController.WORLD_HEIGHT);
            _NoiseShader.SetVector("_MaximumSize", new float4(GenerationConstants.CHUNK_SIZE));
        }

        #endregion


        #region Instance Members

        private CancellationTokenSource _CancellationTokenSource;
        private ConcurrentQueue<BlockAction> _BlockActions;
        private INodeCollection<ushort> _Blocks;
        private List<ChunkController> _Neighbors;
        private Mesh _Mesh;

#if DEBUG

        private WeakReference _BlocksReference;

#endif

        private bool _GenerateNeighbors;
        private long _BlockActionsCount;
        private long _State;
        private long _Active;
        private long _UpdateMesh;

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

        public INodeCollection<ushort> Blocks
        {
            get => _Blocks;
            private set
            {
                _Blocks = value;

#if DEBUG

                _BlocksReference = new WeakReference(_Blocks);

#endif
            }
        }

        public int3 OriginPoint { get; private set; }

        #endregion


        #region Serialized Members

        [SerializeField]
        private Transform SelfTransform;

        [SerializeField]
        private MeshFilter MeshFilter;

        [SerializeField]
        private MeshRenderer MeshRenderer;

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

        private void Awake()
        {
            if (_NoiseShader == null)
            {
                InitializeNoiseShader();
            }

            _Neighbors = new List<ChunkController>();
            _BlockActions = new ConcurrentQueue<BlockAction>();
            _Mesh = new Mesh();

            MeshFilter.sharedMesh = _Mesh;
            MeshRenderer.materials = TextureController.Current.BlockMaterials;
        }

        private void OnEnable()
        {
            OriginPoint = WydMath.ToInt(SelfTransform.position);

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(20, this);

            _CancellationTokenSource = new CancellationTokenSource();
            Active = gameObject.activeSelf;
            _GenerateNeighbors = true;

            BlocksChanged += FlagUpdateMeshCallback;
            TerrainChanged += FlagUpdateMeshCallback;

#if UNITY_EDITOR

            MinimumPoint = WydMath.ToFloat(OriginPoint);
            MaximumPoint = WydMath.ToFloat(OriginPoint + GenerationConstants.CHUNK_SIZE);

#endif
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(20, this);

            if (_Mesh != null)
            {
                _Mesh.Clear();
            }

            _CancellationTokenSource.Cancel();
            _Neighbors.Clear();
            _BlockActions = new ConcurrentQueue<BlockAction>();

            Blocks = null;
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
            Blocks = null;
            _BlockActions = null;

#if DEBUG

            if (_BlocksReference.IsAlive)
            {
                Log.Warning($"{nameof(Blocks)} has not been garbage collected and chunk is being destroyed. This is likely a memory leak.");
            }

#endif

            State = 0;
            UpdateMesh = false;
            BlocksChanged = TerrainChanged = MeshChanged = null;
            Active = false;
        }

        #endregion


        #region De/Activation

        public void Activate(float3 position)
        {
            SelfTransform.position = position;

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        #endregion


        #region Generation

        private void BeginBuilding()
        {
            // use SIZE_SQUARED (to represent full x,z range)
            ComputeBuffer heightmapBuffer = new ComputeBuffer(GenerationConstants.CHUNK_SIZE_SQUARED, 4);
            ComputeBuffer caveNoiseBuffer = new ComputeBuffer(GenerationConstants.CHUNK_SIZE_CUBED, 4);

            _NoiseShader.SetVector("_Offset", new float4(OriginPoint.xyzz));
            _NoiseShader.SetFloat("_Frequency", GenerationConstants.FREQUENCY);
            _NoiseShader.SetFloat("_Persistence", GenerationConstants.PERSISTENCE);
            _NoiseShader.SetFloat("_SurfaceHeight", WorldController.WORLD_HEIGHT / 2f);

            int heightmapKernel = _NoiseShader.FindKernel("Heightmap2D");
            _NoiseShader.SetBuffer(heightmapKernel, "HeightmapResult", heightmapBuffer);

            int caveNoiseKernel = _NoiseShader.FindKernel("CaveNoise3D");
            _NoiseShader.SetBuffer(caveNoiseKernel, "CaveNoiseResult", caveNoiseBuffer);

            _NoiseShader.Dispatch(heightmapKernel, GenerationConstants.CHUNK_THREAD_GROUP_SIZE, 1, GenerationConstants.CHUNK_THREAD_GROUP_SIZE);
            _NoiseShader.Dispatch(caveNoiseKernel, GenerationConstants.CHUNK_THREAD_GROUP_SIZE, GenerationConstants.CHUNK_THREAD_GROUP_SIZE,
                GenerationConstants.CHUNK_THREAD_GROUP_SIZE);

            if (!_BuildingJobs.TryTake(out ChunkBuildingJob chunkBuildingJob))
            {
                chunkBuildingJob = new ChunkBuildingJob();
            }

            chunkBuildingJob.SetData(_CancellationTokenSource.Token, OriginPoint, GenerationConstants.FREQUENCY, GenerationConstants.PERSISTENCE,
                Options.Instance.GPUAcceleration ? heightmapBuffer : null,
                Options.Instance.GPUAcceleration ? caveNoiseBuffer : null);
            chunkBuildingJob.WorkFinished += OnTerrainBuildingFinished;

            AsyncJobScheduler.QueueAsyncJob(chunkBuildingJob);


            void OnTerrainBuildingFinished(object sender, AsyncJob asyncJob)
            {
                Debug.Assert(State == ChunkState.AwaitingBuilding,
                    $"{nameof(State)} should always be in the '{nameof(ChunkState.AwaitingBuilding)}' state when meshing finishes.\r\n"
                    + $"\tremark: see the {nameof(State)} property's xml doc for explanation.");

                ChunkBuildingJob finishedChunkBuildingJob = (ChunkBuildingJob)asyncJob;
                finishedChunkBuildingJob.WorkFinished -= OnTerrainBuildingFinished;

                if (Active)
                {
                    Blocks = finishedChunkBuildingJob.GetGeneratedBlockData();

                    State = State.Next();
                }

                finishedChunkBuildingJob.ClearData();
                _BuildingJobs.TryAdd(finishedChunkBuildingJob);
            }
        }

        private void BeginMeshing()
        {
            if (!_MeshingJobs.TryTake(out ChunkMeshingJob chunkMeshingJob))
            {
                chunkMeshingJob = new ChunkMeshingJob(_CancellationTokenSource.Token, Blocks, _Neighbors.Select(neighbor =>
                    neighbor.Blocks).ToArray(), Options.Instance.AdvancedMeshing);
            }

            chunkMeshingJob.WorkFinished += OnMeshingFinished;

            AsyncJobScheduler.QueueAsyncJob(chunkMeshingJob);


            void OnMeshingFinished(object sender, AsyncJob asyncJob)
            {
                Debug.Assert(State == ChunkState.AwaitingMeshing,
                    $"{nameof(State)} should always be in the '{nameof(ChunkState.AwaitingMeshing)}' state when meshing finishes.\r\n"
                    + $"\tremark: see the {nameof(State)} property's xml doc for  explanation.");

                ChunkMeshingJob finishedChunkMeshingJob = (ChunkMeshingJob)asyncJob;
                finishedChunkMeshingJob.WorkFinished -= OnMeshingFinished;

                if (Active && _Mesh is object)
                {
                    // in this case, the meshing job's data will be cleared and pooled synchronously after the mesh is applied.
                    MainThreadActions.Instance.QueueAction(() =>
                    {
                        finishedChunkMeshingJob.ApplyMeshData(ref _Mesh);
                        finishedChunkMeshingJob.ReleaseResources();

                        return true;
                    });
                }

                State = State.Next();
            }
        }

        #endregion


        #region Block Actions

        public ushort GetBlock(int3 globalPosition) =>
            Blocks?.GetPoint(math.abs(globalPosition - OriginPoint)) ?? BlockController.NullID;

        public bool TryGetBlock(int3 localPosition, out ushort blockId)
        {
            blockId = BlockController.NullID;

            if (math.any(localPosition < 0) || math.any(localPosition >= GenerationConstants.CHUNK_SIZE) || (Blocks == null))
            {
                return false;
            }

            blockId = Blocks.GetPoint(localPosition);
            return true;
        }

        public void PlaceBlock(int3 globalPosition, ushort newBlockId) =>
            AllocateBlockAction(math.abs(globalPosition - OriginPoint), newBlockId);

        private void AllocateBlockAction(int3 localPosition, ushort id)
        {
            if (_BlockActionsPool.TryTake(out BlockAction blockAction))
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
            normals.AddRange(
                WydMath.ToComponents(math.select(float3.zero, new float3(1f), localPosition == (GenerationConstants.CHUNK_SIZE - 1))));
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

        private void FlagUpdateMeshCallback(object sender, ChunkChangedEventArgs args)
        {
            FlagMeshForUpdate();
        }

        #endregion
    }
}
