#region

using System;
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
        private readonly ComputeBuffer _NoiseValuesBuffer;
        private readonly float3 _OriginPoint;
        private readonly float _Frequency;
        private readonly float _Persistence;
        private readonly bool _GpuAcceleration;

        private ChunkTerrainBuilder _Builder;

        public ChunkBuildingJob(CancellationToken cancellationToken, float3 originPoint, float frequency,
            float persistence, bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null)
            : base(cancellationToken)
        {
            _CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken,
                cancellationToken).Token;
            _OriginPoint = originPoint;
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesBuffer = noiseValuesBuffer;
        }

        protected override Task Process()
        {
            ChunkTerrainBuilder builder = new ChunkTerrainBuilder(_CancellationToken, _OriginPoint, _Frequency,
                _Persistence, _GpuAcceleration, _NoiseValuesBuffer);
            builder.TimeMeasuredGenerate();

            // builder has completed execution, so set field
            _Builder = builder;

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!_CancellationToken.IsCancellationRequested)
            {
                DiagnosticsController.Current.RollingNoiseRetrievalTimes.Enqueue(_Builder.NoiseRetrievalTimeSpan);
                DiagnosticsController.Current.RollingTerrainGenerationTimes.Enqueue(_Builder.TerrainGenerationTimeSpan);
            }

            return Task.CompletedTask;
        }

        public void GetGeneratedBlockData(out OctreeNode<ushort> blocks)
        {
            if (_Builder == null)
            {
                throw new NullReferenceException(
                    $"'{nameof(ChunkBuilder)}' is null. This likely indicates the job has not completed execution.");
            }

            _Builder.GetGeneratedBlockData(out blocks);
        }
    }
}
