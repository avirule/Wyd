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

        public TerrainStep CurrentStep
        {
            get
            {
                TerrainStep tmp;

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
        private TerrainStep GenerationStep;

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

            if ((CurrentStep == TerrainStep.Complete)
                || !WorldController.Current.ReadyForGeneration
                || (WorldController.Current.AggregateNeighborsStep(WydMath.ToInt(OriginPoint)) < CurrentStep))
            {
                return;
            }

            ExecuteStep(CurrentStep);
        }

        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            _NoiseBuffer?.Release();
            CurrentStep = (TerrainStep)1;

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

        private void ExecuteStep(TerrainStep step)
        {
            switch (step)
            {
                case TerrainStep.Noise:
                    if (OptionsController.Current.GPUAcceleration)
                    {
                        BeginNoiseGeneration();
                        CurrentStep = CurrentStep.Next();
                        break;
                    }
                    else
                    {
                        CurrentStep = TerrainStep.RawTerrain;
                        break;
                    }

                case TerrainStep.RawTerrain:
                    BeginRawTerrainGeneration();
                    break;
                case TerrainStep.AwaitingRawTerrain:
                case TerrainStep.Complete:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, null);
            }
        }

        private void BeginNoiseGeneration()
        {
            _NoiseBuffer = new ComputeBuffer(WydMath.Product(ChunkController.Size), 4);
            int kernel = _noiseShader.FindKernel("CSMain");
            _noiseShader.SetVector("_Offset", new float4(OriginPoint.xyzz));
            _noiseShader.SetFloat("_Frequency", _FREQUENCY);
            _noiseShader.SetFloat("_Persistence", _PERSISTENCE);
            _noiseShader.SetBuffer(kernel, "Result", _NoiseBuffer);
            // 1024 is the value set in the shader's [numthreads(--> 1024 <--, 1, 1)]
            _noiseShader.Dispatch(kernel, WydMath.Product(ChunkController.Size) / 1024, 1, 1);
        }

        private void BeginRawTerrainGeneration()
        {
            ChunkBuildingJob asyncJob = new ChunkBuildingJob(OriginPoint, ref BlocksController.Blocks,
                _FREQUENCY, _PERSISTENCE, OptionsController.Current.GPUAcceleration, _NoiseBuffer);

            QueueAsyncJob(asyncJob);
        }

        #endregion


        #region EVENTS

        public event ChunkChangedEventHandler TerrainChanged;

        private void OnTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            TerrainChanged?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, AsyncJobEventArgs args)
        {
            OnTerrainChanged(this, new ChunkChangedEventArgs(OriginPoint, Directions.CardinalDirectionAxes));
            args.AsyncJob.WorkFinished -= OnJobFinished;
            CurrentStep = CurrentStep.Next();

#if UNITY_EDITOR

            if (CurrentStep == TerrainStep.Complete)
            {
                TotalTimesTerrainChanged += 1;
                TimesTerrainChanged += 1;
            }
#endif
        }

        #endregion
    }
}
