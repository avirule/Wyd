#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game.World.Chunks.BuildingJob;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Compression;
using Wyd.System.Extensions;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkGenerator
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
            new ObjectCache<ChunkBuildingJobRawTerrain>(true, false, 256);

        private static readonly ObjectCache<ChunkBuildingJobAccents> ChunkAccentsBuilderCache =
            new ObjectCache<ChunkBuildingJobAccents>(true, false, 256);

        private static readonly ObjectCache<ChunkMeshingJob> ChunkMeshersCache =
            new ObjectCache<ChunkMeshingJob>(true);

        private static bool _hasSetupTimeAggregators;

        public static FixedConcurrentQueue<TimeSpan> BuildTimes;
        public static FixedConcurrentQueue<TimeSpan> MeshTimes;

        private bool _IsSet;
        private Bounds _Bounds;
        private Vector3 _Position;
        private LinkedList<RLENode<ushort>> _Blocks;
        private Mesh _Mesh;
        private ComputeShader _NoiseShader;
        private Action _PendingAction;
        private object _JobIdentity;
        private TimeSpan _AggregateBuildTimeSpan;
        private bool _StepForward;
        private bool _Meshed;
        private bool _MeshUpdateRequested;

        public GenerationStep CurrentStep { get; private set; }
        public bool Generating { get; private set; }

        public ChunkGenerator() => _IsSet = false;

        public void Set(Bounds bounds, ref LinkedList<RLENode<ushort>> blocks, ref Mesh mesh)
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
            // cache value to avoid dll calls
            _Position = _Bounds.min;
            _Blocks = blocks;
            _Mesh = mesh;

            if (_NoiseShader == default)
            {
                _NoiseShader = GameController.LoadResource<ComputeShader>(@"Graphics\Shaders\NoiseComputationShader");
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
                || (WorldController.Current.AggregateNeighborsStep(_Position) < CurrentStep))
            {
                return;
            }

            ExecuteStep(CurrentStep);
        }

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
                _NoiseShader.SetVector("_Offset", _Position);
                _NoiseShader.SetFloat("_Frequency", frequency);
                _NoiseShader.SetFloat("_Persistence", persistence);
                _NoiseShader.SetBuffer(kernel, "Result", noiseBuffer);
                // 256 is the value set in the shader's [numthreads(--> 256 <--, 1, 1)]
                _NoiseShader.Dispatch(kernel, ChunkController.Size.Product() / 1024, 1, 1);

                job.Set(_Bounds, ref _Blocks, frequency, persistence, OptionsController.Current.GPUAcceleration,
                    noiseBuffer);
            }
            else
            {
                job.Set(_Bounds, ref _Blocks, frequency, persistence);
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

            job.Set(_Bounds, ref _Blocks);

            QueueJob(job);
        }

        private void BeginGeneratingMesh()
        {
            if (Generating)
            {
                return;
            }

            ChunkMeshingJob job = ChunkMeshersCache.RetrieveItem() ?? new ChunkMeshingJob();

            job.Set(_Bounds, RunLengthCompression.Decompress(_Blocks), true, _Meshed);

            if (QueueJob(job))
            {
                _MeshUpdateRequested = false;
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
            job.SetMesh(ref _Mesh);
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