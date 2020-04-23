#region

using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
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

            if (_NoiseShader == null)
            {
                _NoiseShader = Resources.Load<ComputeShader>(@"Graphics\Shaders\NoiseComputationShader");
                _NoiseShader.SetInt("_NoiseSeed", WorldController.Current.Seed);
                _NoiseShader.SetVector("_MaximumSize", new Vector4(ChunkController.Size3D.x, ChunkController.Size3D.y,
                    ChunkController.Size3D.z, 0f));
                _NoiseShader.SetFloat("_WorldHeight", WorldController.WORLD_HEIGHT);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _NoiseValuesBuffer?.Release();
        }

        public void BeginTerrainGeneration(CancellationToken cancellationToken, AsyncJobEventHandler callback,
            out object jobIdentity)
        {
            _NoiseValuesBuffer = new ComputeBuffer(ChunkController.SIZE_CUBED, 4);
            int kernel = _NoiseShader.FindKernel("CSMain");
            _NoiseShader.SetVector("_Offset", new float4(OriginPoint.xyzz));
            _NoiseShader.SetFloat("_Frequency", _FREQUENCY);
            _NoiseShader.SetFloat("_Persistence", _PERSISTENCE);
            _NoiseShader.SetBuffer(kernel, "Result", _NoiseValuesBuffer);
            // 1024 is the value set in the shader's [numthreads(--> 1024 <--, 1, 1)]
            _NoiseShader.Dispatch(kernel, 1024, 1, 1);

            ChunkTerrainJob asyncJob = new ChunkTerrainBuilderJob(cancellationToken, OriginPoint, _FREQUENCY, _PERSISTENCE, _NoiseValuesBuffer);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            jobIdentity = asyncJob.Identity;

            Task.Run(async () => await AsyncJobScheduler.QueueAsyncJob(asyncJob), cancellationToken);
        }

        public void BeginTerrainDetailing(CancellationToken cancellationToken, AsyncJobEventHandler callback,
            OctreeNode<ushort> blocks, out object jobIdentity)
        {
            ChunkTerrainDetailerJob asyncJob = new ChunkTerrainDetailerJob(cancellationToken, OriginPoint, blocks);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            jobIdentity = asyncJob.Identity;

            Task.Run(async () => await AsyncJobScheduler.QueueAsyncJob(asyncJob), cancellationToken);
        }
    }
}
