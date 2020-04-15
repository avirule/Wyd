#region

using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBuildingJob : AsyncJob
    {
        private readonly CancellationToken _CancellationToken;
        private readonly float3 _OriginPoint;
        private readonly ComputeBuffer _NoiseValuesBuffer;
        private readonly float _Frequency;
        private readonly float _Persistence;
        private readonly bool _GpuAcceleration;

        private OctreeNode _Blocks;
        private ChunkBuilder _Builder;

        public ChunkBuildingJob(CancellationToken cancellationToken, float3 originPoint, ref OctreeNode blocks,
            float frequency, float persistence, bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null) :
            base(cancellationToken)
        {
            _CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken,
                cancellationToken).Token;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesBuffer = noiseValuesBuffer;
        }

        protected override Task Process()
        {
            _Builder = new ChunkBuilder(_CancellationToken, _OriginPoint, ref _Blocks, _Frequency, _Persistence,
                _GpuAcceleration, _NoiseValuesBuffer);
            _Builder.Generate();

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!_CancellationToken.IsCancellationRequested)
            {
                DiagnosticsController.Current.RollingNoiseRetrievalTimes.Enqueue(_Builder
                    .NoiseRetrievalTimeSpan);
                DiagnosticsController.Current.RollingTerrainGenerationTimes.Enqueue(_Builder
                    .TerrainGenerationTimeSpan);
            }

            return Task.CompletedTask;
        }
    }
}
