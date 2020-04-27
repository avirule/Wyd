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
        private const float _FREQUENCY = 0.0075f;
        private const float _PERSISTENCE = 0.6f;
        private static ComputeShader _NoiseShader;

        private ComputeBuffer _HeightmapBuffer;
        private ComputeBuffer _CaveNoiseBuffer;

        protected override void Awake()
        {
            base.Awake();

            _NoiseShader = Resources.Load<ComputeShader>(@"Graphics\Shaders\OpenSimplex");
            _NoiseShader.SetInt("_HeightmapSeed", WorldController.Current.Seed);
            _NoiseShader.SetInt("_CaveNoiseSeedA", WorldController.Current.Seed ^ 2);
            _NoiseShader.SetInt("_CaveNoiseSeedB", WorldController.Current.Seed ^ 3);
            _NoiseShader.SetFloat("_WorldHeight", WorldController.WORLD_HEIGHT);
            _NoiseShader.SetVector("_MaximumSize", new float4(ChunkController.Size3D.xyzz));
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _HeightmapBuffer?.Release();
        }

        private void OnDestroy()
        {
            _HeightmapBuffer?.Dispose();
        }

        public void BeginTerrainGeneration(CancellationToken cancellationToken, AsyncJobEventHandler callback, out object jobIdentity)
        {
            _HeightmapBuffer = new ComputeBuffer(ChunkController.SIZE_SQUARED, 4);
            _CaveNoiseBuffer = new ComputeBuffer(ChunkController.SIZE_CUBED, 4);

            _NoiseShader.SetVector("_Offset", new float4(OriginPoint.xyzz));
            _NoiseShader.SetFloat("_Frequency", _FREQUENCY);
            _NoiseShader.SetFloat("_Persistence", _PERSISTENCE);
            _NoiseShader.SetFloat("_SurfaceHeight", WorldController.WORLD_HEIGHT / 2f);

            int heightmapKernel = _NoiseShader.FindKernel("Heightmap2D");
            _NoiseShader.SetBuffer(heightmapKernel, "HeightmapResult", _HeightmapBuffer);

            int caveNoiseKernel = _NoiseShader.FindKernel("CaveNoise3D");
            _NoiseShader.SetBuffer(caveNoiseKernel, "CaveNoiseResult", _CaveNoiseBuffer);

            _NoiseShader.Dispatch(heightmapKernel, 1024, 1, 1);
            _NoiseShader.Dispatch(caveNoiseKernel, 1024, 1, 1);

            ChunkTerrainJob asyncJob = new ChunkTerrainBuilderJob(cancellationToken, OriginPoint, _FREQUENCY, _PERSISTENCE,
                OptionsController.Current.GPUAcceleration ? _HeightmapBuffer : null,
            OptionsController.Current.GPUAcceleration ? _CaveNoiseBuffer : null);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            jobIdentity = asyncJob.Identity;

            AsyncJobScheduler.QueueAsyncJob(asyncJob);
        }

        public void BeginTerrainDetailing(CancellationToken cancellationToken, AsyncJobEventHandler callback, INodeCollection<ushort> blocks,
            out object jobIdentity)
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
