#region

using System;
using System.Linq;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Game;
using Wyd.Game.World.Chunks;
using Wyd.Game.World.Chunks.BuildingJob;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Extensions;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World
{
    public class ChunkGenerationController : MonoBehaviour
    {
        private static readonly ObjectCache<ChunkBuildingJobRawTerrain> ChunkRawTerrainBuilderCache =
            new ObjectCache<ChunkBuildingJobRawTerrain>(true);

        private static readonly ObjectCache<ChunkBuildingJobAccents> ChunkAccentsBuilderCache =
            new ObjectCache<ChunkBuildingJobAccents>(true);

        private static readonly ObjectCache<ChunkMeshingJob> ChunkMeshersCache =
            new ObjectCache<ChunkMeshingJob>(true);


        #region INSTANCE MEMBERS

        private ChunkBlocksController _ChunkBlocksController;
        private ComputeShader _NoiseShader;
        private Transform _SelfTransform;
        private Bounds _Bounds;
        private Mesh _Mesh;

        private object _JobIdentity;
        private ChunkMeshingJob _PendingMeshData;
        private bool _StepForward;
        private TimeSpan _AggregateBuildTime;
        private GenerationData.MeshingState _MeshingState;

        public GenerationData.GenerationStep CurrentStep { get; private set; }
        public bool Generating { get; private set; }

        #endregion


        #region SERIALIZED MEMBERS

        [SerializeField]
        private MeshFilter MeshFilter;

        #endregion


        public void Awake()
        {
            _ChunkBlocksController = GetComponent<ChunkBlocksController>();

            _NoiseShader = GameController.LoadResource<ComputeShader>(@"Graphics\Shaders\NoiseComputationShader");
            _NoiseShader.SetInt("_NoiseSeed", WorldController.Current.Seed);
            _NoiseShader.SetVector("_MaximumSize",
                new Vector4(ChunkController.Size.x, ChunkController.Size.y,
                    ChunkController.Size.z, 0f));

            _SelfTransform = transform;
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMax(position, position + ChunkController.Size);

            _Mesh = MeshFilter.sharedMesh;
        }

        public void Update()
        {
            if (_StepForward)
            {
                CurrentStep = CurrentStep.Next();
                _StepForward = false;
            }

            switch (_MeshingState)
            {
                case GenerationData.MeshingState.Unmeshed:
                    break;
                case GenerationData.MeshingState.UpdateRequested:
                    if (CurrentStep == GenerationData.GenerationStep.Complete)
                    {
                        CurrentStep = GenerationData.GenerationStep.Meshing;
                    }

                    break;
                case GenerationData.MeshingState.MeshPending:
                    _PendingMeshData?.SetMesh(ref _Mesh);
                    _MeshingState = GenerationData.MeshingState.Meshed;
                    break;
                case GenerationData.MeshingState.Meshed:
                    break;
                case GenerationData.MeshingState.PendingGeneration:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (Generating
                || (CurrentStep == GenerationData.GenerationStep.Complete)
                || (WorldController.Current.AggregateNeighborsStep(_SelfTransform.position) < CurrentStep))
            {
                return;
            }

            ExecuteStep(CurrentStep);
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
        }

        public void Activate()
        {
            _SelfTransform = transform;
            Vector3 position = _SelfTransform.position;
            _Bounds = new Bounds(position, position + ChunkController.Size);

            _JobIdentity = null;
            _PendingMeshData = null;
            _StepForward = false;
            _AggregateBuildTime = default;
            _MeshingState = GenerationData.MeshingState.Unmeshed;
        }

        public void Deactivate()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            _JobIdentity = null;
            _PendingMeshData = null;
            _StepForward = false;
            _AggregateBuildTime = default;
            _MeshingState = GenerationData.MeshingState.Unmeshed;
            StopAllCoroutines();
        }


        #region RUNTIME

        private void ExecuteStep(GenerationData.GenerationStep step)
        {
            switch (step)
            {
                case GenerationData.GenerationStep.RawTerrain:
                    BeginGeneratingRawTerrain();
                    break;
                case GenerationData.GenerationStep.Accents:
                    BeginGeneratingAccents();
                    break;
                case GenerationData.GenerationStep.Meshing:
                    BeginGeneratingMesh();
                    break;
                case GenerationData.GenerationStep.Complete:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, null);
            }
        }

        private bool QueueJob(Job job)
        {
            if (!GameController.Current.TryQueueJob(job, out _JobIdentity))
            {
                return false;
            }

            GameController.Current.JobFinished += OnJobQueueFinishedJob;
            Generating = true;
            return true;
        }

        #endregion

        #region GENERATION STEP METHODS

        private void BeginGeneratingRawTerrain()
        {
            const float frequency = 0.01f;
            const float persistence = -1f;

            if (Generating)
            {
                return;
            }

            ChunkBuildingJobRawTerrain job = ChunkRawTerrainBuilderCache.RetrieveItem()
                                             ?? new ChunkBuildingJobRawTerrain();

            if (OptionsController.Current.GPUAcceleration)
            {
                ComputeBuffer noiseBuffer = new ComputeBuffer(ChunkController.Size.Product(), 4);
                int kernel = _NoiseShader.FindKernel("CSMain");
                _NoiseShader.SetVector("_Offset", _SelfTransform.position);
                _NoiseShader.SetFloat("_Frequency", frequency);
                _NoiseShader.SetFloat("_Persistence", persistence);
                _NoiseShader.SetBuffer(kernel, "Result", noiseBuffer);
                // 1024 is the value set in the shader's [numthreads(--> 1024 <--, 1, 1)]
                _NoiseShader.Dispatch(kernel, ChunkController.Size.Product() / 1024, 1, 1);

                job.Set(_Bounds, ref _ChunkBlocksController.Blocks, frequency, persistence, OptionsController.Current.GPUAcceleration,
                    noiseBuffer);
            }
            else
            {
                job.Set(_Bounds, ref _ChunkBlocksController.Blocks, frequency, persistence);
            }

            QueueJob(job);
        }

        // todo fix this
        public void BeginGeneratingAccents()
        {
            if (Generating)
            {
                return;
            }

            ChunkBuildingJobAccents job = ChunkAccentsBuilderCache.RetrieveItem() ?? new ChunkBuildingJobAccents();

            job.Set(_Bounds, ref _ChunkBlocksController.Blocks);

            QueueJob(job);
        }

        private void BeginGeneratingMesh()
        {
            if (Generating)
            {
                return;
            }

            ChunkMeshingJob job = ChunkMeshersCache.RetrieveItem() ?? new ChunkMeshingJob();

            job.SetData(_Bounds, _ChunkBlocksController.Blocks, true, _MeshingState == GenerationData.MeshingState.Meshed);

            if (QueueJob(job))
            {
                _MeshingState = GenerationData.MeshingState.PendingGeneration;
            }
        }

        #endregion

        public void RequestMeshUpdate()
        {
            _MeshingState = GenerationData.MeshingState.UpdateRequested;
        }

        #region EVENTS

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;
        public event EventHandler<ChunkChangedEventArgs> MeshChanged;

        protected virtual void OnBlocksChanged(ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(this, args);
        }

        protected virtual void OnMeshChanged(ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(this, args);
        }

        private void OnJobQueueFinishedJob(object sender, JobEventArgs args)
        {
            if (args.Job.Identity != _JobIdentity)
            {
                return;
            }

            switch (CurrentStep)
            {
                case GenerationData.GenerationStep.RawTerrain:
                    _AggregateBuildTime += args.Job.ExecutionTime;
                    OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
                    break;
                case GenerationData.GenerationStep.Accents:
                    _AggregateBuildTime += args.Job.ExecutionTime;
                    OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, Enumerable.Empty<Vector3>()));
                    break;
                case GenerationData.GenerationStep.Meshing:
                    if (!(args.Job is ChunkMeshingJob job))
                    {
                        return;
                    }

                    _PendingMeshData = job;
                    _MeshingState = GenerationData.MeshingState.MeshPending;
                    DiagnosticsController.Current.RollingChunkMeshTimes.Enqueue(args.Job.ExecutionTime);
                    OnMeshChanged(new ChunkChangedEventArgs(_Bounds, Enumerable.Empty<Vector3>()));
                    break;
                case GenerationData.GenerationStep.Complete:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // this check always BEFORE incrementing the step
            if (CurrentStep == GenerationData.FINAL_TERRAIN_STEP)
            {
                DiagnosticsController.Current.RollingChunkBuildTimes.Enqueue(_AggregateBuildTime);
                _AggregateBuildTime = TimeSpan.Zero;
            }

            Generating = false;
            _StepForward = true;
            _JobIdentity = null;
            GameController.Current.JobFinished -= OnJobQueueFinishedJob;
        }

        #endregion
    }
}
