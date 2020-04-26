#region

using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Game.World.Chunks;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkTerrainController : ActivationStateChunkController
    {
        private const float _FREQUENCY = 0.01f;
        private const float _PERSISTENCE = -1f;

        private static ComputeShader _NoiseShader;

        private ComputeBuffer _NoiseValuesBuffer;

        protected override void Awake()
        {
            base.Awake();

            _NoiseShader = Resources.Load<ComputeShader>(@"Graphics\Shaders\OpenSimplex3D");
            _NoiseShader.SetInt("_NoiseSeed", WorldController.Current.Seed);
            _NoiseShader.SetInt("_WorldHeight", WorldController.WORLD_HEIGHT);
            _NoiseShader.SetFloat("_Frequency", _FREQUENCY);
            _NoiseShader.SetFloat("_Persistence", _PERSISTENCE);
            _NoiseShader.SetVector("_MaximumSize", new float4(ChunkController.Size3D.xyzz));
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _NoiseShader.SetVector("_Offset", new float4(OriginPoint.xyzz));
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _NoiseValuesBuffer?.Release();
        }

        private void OnDestroy()
        {
            _NoiseValuesBuffer?.Dispose();
        }

        public void BeginTerrainGeneration(CancellationToken cancellationToken, AsyncJobEventHandler callback, out object jobIdentity)
        {
            _NoiseValuesBuffer = new ComputeBuffer(ChunkController.SIZE_CUBED, 4);
            int kernel = _NoiseShader.FindKernel("CSMain");
            _NoiseShader.SetBuffer(kernel, "Result", _NoiseValuesBuffer);
            // 1024 is the value set in the shader's [numthreads(--> 1024 <--, 1, 1)]
            _NoiseShader.Dispatch(kernel, 1024, 1, 1);

            ChunkTerrainJob asyncJob = new ChunkTerrainBuilderJob(cancellationToken, OriginPoint, _FREQUENCY, _PERSISTENCE,
                OptionsController.Current.GPUAcceleration ? _NoiseValuesBuffer : null);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            jobIdentity = asyncJob.Identity;

            AsyncJobScheduler.QueueAsyncJob(asyncJob);
        }

        public void BeginTerrainDetailing(CancellationToken cancellationToken, AsyncJobEventHandler callback,
            INodeCollection<ushort> blocks, out object jobIdentity)
        {
            ChunkTerrainDetailerJob asyncJob = new ChunkTerrainDetailerJob(cancellationToken, OriginPoint, blocks);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            jobIdentity = asyncJob.Identity;

            AsyncJobScheduler.QueueAsyncJob(asyncJob);
        }
    }
}
