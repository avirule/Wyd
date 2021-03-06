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
using Wyd.Collections;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Diagnostics;
using Wyd.Extensions;
using Wyd.Jobs;
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
        BuildingDispatched,
        Mesh,
        MeshingDispatched,
        Meshed
    }

    public class ChunkController : MonoBehaviour, IPerFrameIncrementalUpdate
    {
        private static readonly ObjectPool<BlockAction> _BlockActionsPool = new ObjectPool<BlockAction>(1024);
        private static readonly ObjectPool<ChunkTerrainBuilderJob> _TerrainBuilderJobs = new ObjectPool<ChunkTerrainBuilderJob>();
        private static readonly ObjectPool<ChunkMeshingJob> _MeshingJobs = new ObjectPool<ChunkMeshingJob>();


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
        private List<ChunkController> _Neighbors;
        private Mesh _Mesh;

        private bool _CacheNeighbors;
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

        public INodeCollection<ushort> Blocks { get; private set; }

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

        /// <summary>
        ///     Total numbers of times chunk has been
        ///     meshed, persisting through de/activation.
        /// </summary>
        [SerializeField]
        [ReadOnlyInspectorField]
        private long TotalTimesMeshed;

        /// <summary>
        ///     Number of times chunk has been meshed,
        ///     resetting every de/activation.
        /// </summary>
        [SerializeField]
        [ReadOnlyInspectorField]
        private long TimesMeshed;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long VertexCount;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long TrianglesCount;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long UVsCount;

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
            _CacheNeighbors = true;

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

#if UNITY_EDITOR

            TimesMeshed = VertexCount = TrianglesCount = UVsCount = 0;

#endif

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
                    BeginBuilding();

                    State = State.Next();
                    break;
                case ChunkState.BuildingDispatched:
                    break;
                // case ChunkState.Undetailed:
                //     TerrainController.BeginDetailing(_CancellationTokenSource.Token, OnTerrainDetailingFinished, _Blocks, out _TerrainJobIdentity);
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
                        BeginMeshing();
                        State = State.Next();
                    }

                    UpdateMesh = false;

                    break;
                case ChunkState.MeshingDispatched:
                    break;
                case ChunkState.Meshed:
                    if (UpdateMesh && _BlockActions.IsEmpty)
                    {
                        State = ChunkState.Mesh;
                    }

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

            ChunkTerrainBuilderJob terrainBuilderJob = new ChunkTerrainBuilderJob();
            terrainBuilderJob.SetData(_CancellationTokenSource.Token, OriginPoint, GenerationConstants.FREQUENCY, GenerationConstants.PERSISTENCE,
                Options.Instance.GPUAcceleration ? heightmapBuffer : null,
                Options.Instance.GPUAcceleration ? caveNoiseBuffer : null);

            void OnTerrainBuildingFinished(object sender, AsyncJob asyncJob)
            {
                Debug.Assert(State == ChunkState.BuildingDispatched,
                    $"{nameof(State)} should always be in the '{nameof(ChunkState.BuildingDispatched)}' state when meshing finishes.\r\n"
                    + $"\tremark: see the {nameof(State)} property's xmldoc for explanation.");

                asyncJob.WorkFinished -= OnTerrainBuildingFinished;

                if (Active)
                {
                    Blocks = terrainBuilderJob.GetGeneratedBlockData();

                    State = State.Next();
                }

                terrainBuilderJob.ClearData();
                _TerrainBuilderJobs.TryAdd(terrainBuilderJob);
            }

            terrainBuilderJob.WorkFinished += OnTerrainBuildingFinished;

            AsyncJobScheduler.QueueAsyncJob(terrainBuilderJob);
        }

        private void BeginMeshing()
        {
            // todo make setting for improved meshing
            ChunkMeshingJob meshingJob = _MeshingJobs.Retrieve() ?? new ChunkMeshingJob();
            meshingJob.SetData(_CancellationTokenSource.Token, OriginPoint, Blocks, true);

            void OnMeshingFinished(object sender, AsyncJob asyncJob)
            {
                Debug.Assert(State == ChunkState.MeshingDispatched,
                    $"{nameof(State)} should always be in the '{nameof(ChunkState.MeshingDispatched)}' state when meshing finishes.\r\n"
                    + $"\tremark: see the {nameof(State)} property's xmldoc for  explanation.");

                asyncJob.WorkFinished -= OnMeshingFinished;

                if (Active)
                {
                    // in this case, the meshing job's data will be cleared and pooled synchronously, after the mesh is applied.
                    MainThreadActionsController.Current.QueueAction(() => ApplyMesh(meshingJob));
                }
                else
                {
                    meshingJob.ClearData();
                    _MeshingJobs.TryAdd(meshingJob);
                }


                State = State.Next();
            }

            meshingJob.WorkFinished += OnMeshingFinished;

            AsyncJobScheduler.QueueAsyncJob(meshingJob);
        }

        private bool ApplyMesh(ChunkMeshingJob meshingJob)
        {
            meshingJob.ApplyMeshData(ref _Mesh);
            meshingJob.ClearData();
            _MeshingJobs.TryAdd(meshingJob);

#if UNITY_EDITOR

            //VertexCount = _Mesh.vertices.Length;
            //TrianglesCount = _Mesh.triangles.Length;
            //UVsCount = _Mesh.uv.Length;
            TotalTimesMeshed += 1;
            TimesMeshed += 1;

#endif


            return true;
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
            if (_BlockActionsPool.TryRetrieve(out BlockAction blockAction))
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
