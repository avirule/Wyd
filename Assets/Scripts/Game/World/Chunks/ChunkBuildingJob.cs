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

        private OctreeNode<ushort> _Blocks;
        private ChunkRawTerrainBuilder _TerrainBuilder;

        public ChunkBuildingJob(CancellationToken cancellationToken, float3 originPoint, ref OctreeNode<ushort> blocks,
            float frequency, float persistence, bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null) :
            base(cancellationToken)
        {
            _CancellationToken = cancellationToken;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesBuffer = noiseValuesBuffer;
        }

        protected override Task Process()
        {
            _TerrainBuilder = new ChunkRawTerrainBuilder(_CancellationToken, _OriginPoint, ref _Blocks, _Frequency,
                _Persistence, _GpuAcceleration, _NoiseValuesBuffer);
            _TerrainBuilder.Generate();

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!_CancellationToken.IsCancellationRequested)
            {
                DiagnosticsController.Current.RollingNoiseRetrievalTimes.Enqueue(_TerrainBuilder.NoiseRetrievalTimeSpan);
                DiagnosticsController.Current.RollingTerrainGenerationTimes.Enqueue(_TerrainBuilder
                    .TerrainGenerationTimeSpan);
            }

            return Task.CompletedTask;
        }
    }
}
