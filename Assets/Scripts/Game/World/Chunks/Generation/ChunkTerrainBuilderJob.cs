#region

using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.System;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public class ChunkTerrainBuilderJob : ChunkTerrainJob
    {
        private readonly float _Frequency;
        private readonly float _Persistence;
        private readonly ComputeBuffer _HeightmapBuffer;
        private readonly ComputeBuffer _CaveNoiseBuffer;

        public ChunkTerrainBuilderJob(CancellationToken cancellationToken, int3 originPoint, float frequency,
            float persistence, ComputeBuffer heightmapBuffer = null, ComputeBuffer caveNoiseBuffer = null)
            : base(cancellationToken, originPoint)
        {
            _Frequency = frequency;
            _Persistence = persistence;
            _HeightmapBuffer = heightmapBuffer;
            _CaveNoiseBuffer = caveNoiseBuffer;
        }

        protected override Task Process()
        {
            ChunkTerrainBuilder builder = new ChunkTerrainBuilder(CancellationToken, OriginPoint, _Frequency, _Persistence, _HeightmapBuffer,
                _CaveNoiseBuffer);
            builder.TimeMeasuredGenerate();

            // builder has completed execution, so set field
            _TerrainOperator = builder;

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!CancellationToken.IsCancellationRequested)
            {
                ChunkTerrainBuilder builder = (ChunkTerrainBuilder)_TerrainOperator;

                DiagnosticsController.Current.RollingNoiseRetrievalTimes.Enqueue(builder.NoiseRetrievalTimeSpan);
                DiagnosticsController.Current.RollingTerrainBuildingTimes.Enqueue(builder.TerrainGenerationTimeSpan);
            }

            return Task.CompletedTask;
        }
    }
}
