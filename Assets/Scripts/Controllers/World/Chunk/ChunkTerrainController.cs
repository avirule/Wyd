#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game;
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

        private static ComputeShader _noiseShader;

        #region INSTANCE MEMBERS

        private object _TerrainStepHandle;
        private CancellationTokenSource _CancellationTokenSource;

        public TerrainStep TerrainStep
        {
            get
            {
                TerrainStep tmp;

                lock (_TerrainStepHandle)
                {
                    tmp = _TerrainStep;
                }

                return tmp;
            }
            private set
            {
                lock (_TerrainStepHandle)
                {
                    _TerrainStep = value;
                }
            }
        }

        #endregion

        #region SERIALIZED MEMBERS

        [SerializeField]
        private ChunkBlocksController BlocksController;

        [SerializeField]
        private TerrainStep _TerrainStep;

#if UNITY_EDITOR

        [SerializeField]
        [ReadOnlyInspectorField]
        private long TotalTimesTerrainChanged;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long TimesTerrainChanged;

#endif

        #endregion

        protected override void Awake()
        {
            base.Awake();

            _TerrainStepHandle = new object();

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

            _CancellationTokenSource?.Cancel();
        }

        public void FrameUpdate()
        {
            if ((TerrainStep == TerrainStep.Complete)
                || !WorldController.Current.ReadyForGeneration
                || (WorldController.Current.AggregateNeighborsStep(WydMath.ToInt(OriginPoint)) < TerrainStep))
            {
                return;
            }

            ExecuteStep(TerrainStep);
        }

        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            TerrainStep = (TerrainStep)1;

#if UNITY_EDITOR

            TimesTerrainChanged = 0;

#endif
        }

        #endregion

        #region RUNTIME

        private void QueueAsyncJob(AsyncJob asyncJob)
        {
            asyncJob.WorkFinished += OnJobFinished;

            Task.Run(async () => await AsyncJobScheduler.QueueAsyncJob(asyncJob));

            TerrainStep = TerrainStep.Next();
        }

        private void ExecuteStep(TerrainStep step)
        {
            switch (step)
            {
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

        private void BeginRawTerrainGeneration()
        {
            ChunkBuildingJob asyncJob;

            _CancellationTokenSource?.Cancel();
            _CancellationTokenSource = new CancellationTokenSource();

            if (OptionsController.Current.GPUAcceleration)
            {
                ComputeBuffer noiseBuffer = new ComputeBuffer(WydMath.Product(ChunkController.Size), 4);
                int kernel = _noiseShader.FindKernel("CSMain");
                _noiseShader.SetVector("_Offset", new float4(OriginPoint.xyzz));
                _noiseShader.SetFloat("_Frequency", _FREQUENCY);
                _noiseShader.SetFloat("_Persistence", _PERSISTENCE);
                _noiseShader.SetBuffer(kernel, "Result", noiseBuffer);
                // 1024 is the value set in the shader's [numthreads(--> 1024 <--, 1, 1)]
                _noiseShader.Dispatch(kernel, WydMath.Product(ChunkController.Size) / 1024, 1, 1);

                asyncJob = new ChunkBuildingJob(_CancellationTokenSource.Token, OriginPoint,
                    ref BlocksController.Blocks, _FREQUENCY, _PERSISTENCE, OptionsController.Current.GPUAcceleration,
                    noiseBuffer);
            }
            else
            {
                asyncJob = new ChunkBuildingJob(_CancellationTokenSource.Token, OriginPoint,
                    ref BlocksController.Blocks, _FREQUENCY, _PERSISTENCE);
            }

            QueueAsyncJob(asyncJob);
        }

        #endregion


        #region EVENTS

        public event ChunkChangedEventHandler TerrainChanged;

        private void OnLocalTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            TerrainChanged?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, AsyncJobEventArgs args)
        {
            OnLocalTerrainChanged(this, new ChunkChangedEventArgs(OriginPoint, Directions.AllDirectionAxes));
            args.AsyncJob.WorkFinished -= OnJobFinished;
            TerrainStep = TerrainStep.Next();

#if UNITY_EDITOR

            if (TerrainStep == TerrainStep.Complete)
            {
                TotalTimesTerrainChanged += 1;
                TimesTerrainChanged += 1;
            }
#endif
        }

        #endregion
    }
}
