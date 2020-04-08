#region

using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game;
using Wyd.Game.World;
using Wyd.Game.World.Chunks;
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

        private static ComputeShader _noiseShader;

        #region INSTANCE MEMBERS

        private object _GenerationStepHandle;
        private ComputeBuffer _NoiseBuffer;

        public GenerationData.GenerationStep CurrentStep
        {
            get
            {
                GenerationData.GenerationStep tmp;

                lock (_GenerationStepHandle)
                {
                    tmp = GenerationStep;
                }

                return tmp;
            }
            private set
            {
                lock (_GenerationStepHandle)
                {
                    GenerationStep = value;
                }
            }
        }

        #endregion

        #region SERIALIZED MEMBERS

        [SerializeField]
        private ChunkBlocksController BlocksController;

        [SerializeField]
        private GenerationData.GenerationStep GenerationStep;

#if UNITY_EDITOR

        [SerializeField]
        [ReadOnlyInspectorField]
        private long TotalTimesTerrainChanged;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long TimesTerrainChanged;

        [SerializeField]
        private bool StepIntoFrameUpdate;

#endif

        #endregion

        protected override void Awake()
        {
            base.Awake();

            _GenerationStepHandle = new object();

            if (_noiseShader == null)
            {
                _noiseShader = SystemController.LoadResource<ComputeShader>(@"Graphics\Shaders\NoiseComputationShader");
                _noiseShader.SetInt("_NoiseSeed", WorldController.Current.Seed);
                _noiseShader.SetVector("_MaximumSize", new Vector4(ChunkController.Size.x, ChunkController.Size.y,
                    ChunkController.Size.z, 0f));
                _noiseShader.SetFloat("_WorldHeight", WorldController.WORLD_HEIGHT);
            }
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
#if UNITY_EDITOR

            if (StepIntoFrameUpdate)
            {
                StepIntoFrameUpdate = false;
            }

#endif

            if ((CurrentStep == GenerationData.GenerationStep.Complete)
                || !WorldController.Current.ReadyForGeneration
                || (WorldController.Current.AggregateNeighborsStep(WydMath.ToInt(_Volume.MinPoint)) < CurrentStep))
            {
                return;
            }

            ExecuteStep(CurrentStep);
        }

        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            _NoiseBuffer?.Release();
            CurrentStep = (GenerationData.GenerationStep)1;

#if UNITY_EDITOR

            TimesTerrainChanged = 0;

#endif
        }

        #endregion

        #region RUNTIME

        private void QueueAsyncJob(AsyncJob asyncJob)
        {
            asyncJob.WorkFinished += OnJobFinished;

            Task.Run(async () => await JobScheduler.QueueAsyncJob(asyncJob));

            CurrentStep = CurrentStep.Next();
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
                        break;
                    }
                    else
                    {
                        CurrentStep = GenerationData.GenerationStep.RawTerrain;
                        break;
                    }

                case GenerationData.GenerationStep.RawTerrain:
                    BeginRawTerrainGeneration();
                    break;
                case GenerationData.GenerationStep.AwaitingRawTerrain:
                case GenerationData.GenerationStep.Complete:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, null);
            }
        }

        private void BeginNoiseGeneration()
        {
            _NoiseBuffer = new ComputeBuffer(WydMath.Product(ChunkController.Size), 4);
            int kernel = _noiseShader.FindKernel("CSMain");
            _noiseShader.SetVector("_Offset", new float4(_Volume.MinPoint.xyzz));
            _noiseShader.SetFloat("_Frequency", _FREQUENCY);
            _noiseShader.SetFloat("_Persistence", _PERSISTENCE);
            _noiseShader.SetBuffer(kernel, "Result", _NoiseBuffer);
            // 1024 is the value set in the shader's [numthreads(--> 1024 <--, 1, 1)]
            _noiseShader.Dispatch(kernel, WydMath.Product(ChunkController.Size) / 1024, 1, 1);
        }

        private void BeginRawTerrainGeneration()
        {
            ChunkBuildingJob asyncJob = new ChunkBuildingJob(new GenerationData(_Volume, BlocksController.Blocks),
                _FREQUENCY, _PERSISTENCE, OptionsController.Current.GPUAcceleration, _NoiseBuffer);

            QueueAsyncJob(asyncJob);
        }

        #endregion


        #region EVENTS

        public event ChunkChangedEventHandler TerrainChanged;

        private void OnChunkTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            TerrainChanged?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, AsyncJobEventArgs args)
        {
            OnChunkTerrainChanged(this, new ChunkChangedEventArgs(_Volume, Directions.CardinalDirectionAxes));
            args.AsyncJob.WorkFinished -= OnJobFinished;
            CurrentStep = CurrentStep.Next();

#if UNITY_EDITOR

            if (CurrentStep == GenerationData.GenerationStep.Complete)
            {
                TotalTimesTerrainChanged += 1;
                TimesTerrainChanged += 1;
            }
#endif
        }

        #endregion
    }
}
