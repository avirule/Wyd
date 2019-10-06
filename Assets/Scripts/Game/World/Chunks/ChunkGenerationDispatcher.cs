#region

using System;
using System.Linq;
using Collections;
using Controllers.State;
using Controllers.World;
using Extensions;
using Game.World.Blocks;
using Game.World.Chunks.BuildingJob;
using Jobs;
using UnityEngine;

#endregion

namespace Game.World.Chunks
{
    public class ChunkGenerationDispatcher
    {
        [Flags]
        public enum GenerationStep : ushort
        {
            RawTerrain = 0b0000_0000_0000_0001,
            Accents = 0b0000_0000_0000_0011,
            Meshing = 0b0000_0001_1111_1111,
            Complete = 0b1111_1111_1111_1111
        }

        public const GenerationStep MINIMUM_STEP = GenerationStep.RawTerrain;
        public const GenerationStep LAST_BUILDING_STEP = GenerationStep.Accents;

        private static readonly ObjectCache<ChunkBuildingJobRawTerrain> ChunkRawTerrainBuilderCache =
            new ObjectCache<ChunkBuildingJobRawTerrain>(true);

        private static readonly ObjectCache<ChunkBuildingJobAccents> ChunkAccentsBuilderCache =
            new ObjectCache<ChunkBuildingJobAccents>(true);

        private static readonly ObjectCache<ChunkMeshingJob> ChunkMeshersCache =
            new ObjectCache<ChunkMeshingJob>(true);

        private static bool _hasSetupTimeAggregators;

        public static FixedConcurrentQueue<TimeSpan> BuildTimes;
        public static FixedConcurrentQueue<TimeSpan> MeshTimes;

        private bool _IsSet;
        private Bounds _Bounds;
        private Vector3 _Position;
        private Block[] _Blocks;
        private Mesh _MeshData;
        private ComputeShader _NoiseShader;
        private Action _PendingAction;
        private object _JobIdentity;
        private TimeSpan _AggregateBuildTimeSpan;
        private bool _StepForward;
        private bool _Meshed;
        private bool _MeshUpdateRequested;

        public GenerationStep CurrentStep { get; private set; }
        public bool Generating { get; private set; }

        public ChunkGenerationDispatcher() => _IsSet = false;

        public void Set(Bounds bounds, Block[] blocks, ref Mesh mesh)
        {
            if (!_hasSetupTimeAggregators)
            {
                BuildTimes =
                    new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
                MeshTimes =
                    new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);

                _hasSetupTimeAggregators = true;
            }

            _Bounds = bounds;
            _Position = _Bounds.min;
            _Blocks = blocks;
            _MeshData = mesh;

            if (_NoiseShader == default)
            {
                _NoiseShader = Resources.Load<ComputeShader>(@"Graphics\Shaders\NoiseComputationShader");
                _NoiseShader.SetInt("_NoiseSeed", WorldController.Current.Seed);
                _NoiseShader.SetVector("_MaximumSize",
                    new Vector4(ChunkController.Size.x, ChunkController.Size.y,
                        ChunkController.Size.z, 0f));
            }

            _PendingAction = null;
            _JobIdentity = null;
            _AggregateBuildTimeSpan = TimeSpan.Zero;
            CurrentStep = MINIMUM_STEP;
            _StepForward = _Meshed = _MeshUpdateRequested = Generating = false;
            _IsSet = true;
        }

        public void Unset()
        {
            _JobIdentity = null;
            _IsSet = false;
        }

        public void SynchronousContextUpdate()
        {
            if (!_IsSet)
            {
                return;
            }

            // required, as stepping forward in a threaded context (i.e. the job finished callback) causes issues.
            if (_StepForward)
            {
                CurrentStep = CurrentStep.Next();
                _StepForward = false;
            }

            if (_PendingAction != null)
            {
                _PendingAction.Invoke();
                _PendingAction = null;
            }

            if (_MeshUpdateRequested && (CurrentStep == GenerationStep.Complete))
            {
                CurrentStep = GenerationStep.Meshing;
            }

            if (Generating
                || (CurrentStep == GenerationStep.Complete)
                || (AggregateChunkRegionGenerationStep() < CurrentStep))
            {
                return;
            }

            ExecuteStep(CurrentStep);
        }

        private GenerationStep AggregateChunkRegionGenerationStep() =>
            WorldController.Current.TryGetChunkAt(_Position, out ChunkController chunkController)
                ? chunkController.AggregateGenerationStep
                : GenerationStep.Complete;

        private void ExecuteStep(GenerationStep step)
        {
            switch (step)
            {
                case GenerationStep.RawTerrain:
                    BeginGeneratingRawTerrain();
                    break;
                case GenerationStep.Accents:
                    BeginGeneratingAccents();
                    break;
                case GenerationStep.Meshing:
                    BeginGeneratingMesh();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, null);
            }
        }

        private void BeginGeneratingRawTerrain()
        {
            const float frequency = 0.01f;
            const float persistence = -1f;

            if (Generating || !ChunkRawTerrainBuilderCache.TryRetrieveItem(out ChunkBuildingJobRawTerrain job))
            {
                return;
            }

            if (OptionsController.Current.GPUAcceleration)
            {
                ComputeBuffer noiseBuffer = new ComputeBuffer(ChunkController.Size.Product(), 4);
                int kernel = _NoiseShader.FindKernel("CSMain");
                _NoiseShader.SetVector("_Offset", _Position);
                _NoiseShader.SetFloat("_Frequency", frequency);
                _NoiseShader.SetFloat("_Persistence", persistence);
                _NoiseShader.SetBuffer(kernel, "Result", noiseBuffer);
                // 256 is the value set in the shader's [numthreads(--> 256 <--, 1, 1)]
                _NoiseShader.Dispatch(kernel, ChunkController.Size.Product() / 1024, 1, 1);

                job.Set(_Bounds, _Blocks, frequency, persistence, OptionsController.Current.GPUAcceleration,
                    noiseBuffer);
            }
            else
            {
                job.Set(_Bounds, _Blocks, frequency, persistence);
            }

            QueueJob(job);
        }

        public void BeginGeneratingAccents()
        {
            if (Generating || !ChunkAccentsBuilderCache.TryRetrieveItem(out ChunkBuildingJobAccents job))
            {
                return;
            }

            job.Set(_Bounds, _Blocks);

            QueueJob(job);
        }

        private void BeginGeneratingMesh()
        {
            if (Generating || !ChunkMeshersCache.TryRetrieveItem(out ChunkMeshingJob job))
            {
                return;
            }

            job.Set(_Bounds, _Blocks, true, _Meshed);

            _MeshUpdateRequested = false;

            QueueJob(job);
        }

        private void QueueJob(Job job)
        {
            if (!GameController.Current.TryQueueJob(job, out _JobIdentity))
            {
                return;
            }

            GameController.Current.JobFinished += OnJobQueueFinishedJob;
            Generating = true;
        }

        /// <summary>
        ///     BE VERY CAREFUL USING THIS. INTERRUPTING GENERATION
        ///     STEPS CAN CAUSE UNPREDICTABLE RESULTS DUE TO
        ///     THREADED CONTEXTS.
        /// </summary>
        /// <param name="skip"></param>
        public void SkipBuilding(bool skip)
        {
            if (skip)
            {
                CurrentStep = GenerationStep.Meshing;
            }
        }

        public void RequestMeshUpdate()
        {
            _MeshUpdateRequested = true;
        }

        #region HELPER METHODS

        private void ApplyMesh(ChunkMeshingJob job)
        {
            job.SetMesh(ref _MeshData);
        }

        #endregion

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
                case GenerationStep.RawTerrain:
                    _AggregateBuildTimeSpan += args.Job.ExecutionTime;
                    OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
                    break;
                case GenerationStep.Accents:
                    _AggregateBuildTimeSpan += args.Job.ExecutionTime;
                    OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, Enumerable.Empty<Vector3>()));
                    break;
                case GenerationStep.Meshing:
                    if (!(args.Job is ChunkMeshingJob job))
                    {
                        return;
                    }

                    _Meshed = true;
                    _PendingAction = () => ApplyMesh(job);
                    MeshTimes.Enqueue(args.Job.ExecutionTime);
                    OnMeshChanged(new ChunkChangedEventArgs(_Bounds, Enumerable.Empty<Vector3>()));
                    break;
            }

            // this check always BEFORE incrementing the step
            if (CurrentStep == LAST_BUILDING_STEP)
            {
                BuildTimes.Enqueue(_AggregateBuildTimeSpan);
                _AggregateBuildTimeSpan = TimeSpan.Zero;
            }

            Generating = false;
            _StepForward = true;
            _JobIdentity = null;
            GameController.Current.JobFinished -= OnJobQueueFinishedJob;
        }

        #endregion
    }
}
