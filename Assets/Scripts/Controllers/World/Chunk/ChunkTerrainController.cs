#region

using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking.Match;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Extensions;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkTerrainController : ActivationStateChunkController, IPerFrameUpdate
    {
        private const float _FREQUENCY = 0.01f;
        private const float _PERSISTENCE = -1f;

        public const float THRESHOLD = 0.01f;

        #region INSTANCE MEMBERS

        private ComputeShader _NoiseShader;
        private ComputeBuffer _NoiseBuffer;

        private object _JobIdentity;

        public GenerationData.GenerationStep CurrentStep { get; private set; }
        public bool Generating { get; private set; }

        #endregion

        #region SERIALIZED MEMBERS

        [SerializeField]
        private ChunkBlocksController BlocksController;

        [SerializeField]
        [ReadOnlyInspectorField]
        private int TotalTimesTerrainChanged;

        [SerializeField]
        [ReadOnlyInspectorField]
        private int TimesTerrainChanged;

        #endregion

        protected override void Awake()
        {
            base.Awake();

            _NoiseShader = GameController.LoadResource<ComputeShader>(@"Graphics\Shaders\NoiseComputationShader");
            _NoiseShader.SetInt("_NoiseSeed", WorldController.Current.Seed);
            _NoiseShader.SetVector("_MaximumSize", new Vector4(ChunkController.Size.x, ChunkController.Size.y,
                ChunkController.Size.z, 0f));
            _NoiseShader.SetFloat("_WorldHeight", WorldController.WORLD_HEIGHT);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(40, this);
            ClearInternalData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(40, this);
            ClearInternalData();
        }

        public void FrameUpdate()
        {
            if (Generating
                || (CurrentStep == GenerationData.GenerationStep.Complete)
                || ((CurrentStep > GenerationData.GenerationStep.RawTerrain)
                    && (WorldController.Current.AggregateNeighborsStep(WydMath.ToInt(_Bounds.MinPoint)) < CurrentStep)))
            {
                return;
            }

            ExecuteStep(CurrentStep);
        }

        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            _NoiseBuffer?.Release();
            TimesTerrainChanged = 0;
            _JobIdentity = null;
            CurrentStep = 0;
            Generating = false;
        }

        #endregion

        #region RUNTIME

        private void QueueJob(Job job)
        {
            if (!SystemController.Current.TryQueueJob(job, out _JobIdentity))
            {
                return;
            }

            SystemController.Current.JobFinished += OnJobFinished;
            Generating = true;
        }

        private void ExecuteStep(GenerationData.GenerationStep step)
        {
            switch (step)
            {
                case GenerationData.GenerationStep.Noise:
                    if (OptionsController.Current.GPUAcceleration)
                    {
                        BeginNoiseGeneration();
                        CurrentStep = CurrentStep.Next();
                    }
                    else
                    {
                        CurrentStep = GenerationData.GenerationStep.RawTerrain;
                    }

                    break;
                case GenerationData.GenerationStep.NoiseWaitFrameOne:
                case GenerationData.GenerationStep.NoiseWaitFrameTwo:
                    CurrentStep = CurrentStep.Next();
                    break;
                case GenerationData.GenerationStep.RawTerrain:
                    BeginRawTerrainGeneration();
                    break;
                case GenerationData.GenerationStep.Complete:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, null);
            }
        }

        private void BeginNoiseGeneration()
        {
            _NoiseBuffer = new ComputeBuffer(WydMath.Product(ChunkController.Size), 4);
            int kernel = _NoiseShader.FindKernel("CSMain");
            _NoiseShader.SetVector("_Offset", new float4(_Bounds.MinPoint.xyzz));
            _NoiseShader.SetFloat("_Frequency", _FREQUENCY);
            _NoiseShader.SetFloat("_Persistence", _PERSISTENCE);
            _NoiseShader.SetBuffer(kernel, "Result", _NoiseBuffer);
            // 1024 is the value set in the shader's [numthreads(--> 1024 <--, 1, 1)]
            _NoiseShader.Dispatch(kernel, WydMath.Product(ChunkController.Size) / 1024, 1, 1);
        }

        private void BeginRawTerrainGeneration()
        {
            if (Generating)
            {
                return;
            }

            ChunkBuildingJob job = new ChunkBuildingJob(new GenerationData(_Bounds, BlocksController.Blocks),
                _FREQUENCY, _PERSISTENCE, OptionsController.Current.GPUAcceleration, _NoiseBuffer);

            QueueJob(job);
        }

        #endregion


        #region EVENTS

        public event ChunkChangedEventHandler TerrainChanged;

        private void OnChunkTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            TerrainChanged?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, JobEventArgs args)
        {
            if (args.Job.Identity != _JobIdentity)
            {
                return;
            }

            switch (CurrentStep)
            {
                case GenerationData.GenerationStep.RawTerrain:
                    OnChunkTerrainChanged(this,
                        new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionAxes));
                    break;
                // case GenerationData.GenerationStep.Accents:
                //     _AggregateBuildTime += args.Job.ExecutionTime;
                //     OnChunkTerrainChanged(this, new ChunkChangedEventArgs(_Bounds, Enumerable.Empty<Vector3>()));
                //     break;
                case GenerationData.GenerationStep.Complete:
                case GenerationData.GenerationStep.Noise:
                case GenerationData.GenerationStep.NoiseWaitFrameOne:
                case GenerationData.GenerationStep.NoiseWaitFrameTwo:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // this check always BEFORE incrementing the step
            if (CurrentStep == GenerationData.FINAL_TERRAIN_STEP)
            {
                TotalTimesTerrainChanged += 1;
                TimesTerrainChanged += 1;
            }

            CurrentStep = CurrentStep.Next();
            Generating = false;
            _JobIdentity = null;
            SystemController.Current.JobFinished -= OnJobFinished;
        }

        #endregion
    }
}
