#region

using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.System;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public class ChunkBuildingJob : ChunkTerrainJob
    {
        private float _Frequency;
        private float _Persistence;
        private ComputeBuffer _HeightmapBuffer;
        private ComputeBuffer _CaveNoiseBuffer;

        public void SetData(CancellationToken cancellationToken, int3 originPoint, float frequency,
            float persistence, ComputeBuffer heightmapBuffer = null, ComputeBuffer caveNoiseBuffer = null)
        {
            SetData(cancellationToken, originPoint);
            _Frequency = frequency;
            _Persistence = persistence;
            _HeightmapBuffer = heightmapBuffer;
            _CaveNoiseBuffer = caveNoiseBuffer;
        }

        protected override Task Process()
        {
            ChunkTerrainBuilder builder = new ChunkTerrainBuilder(CancellationToken, _OriginPoint, _Frequency, _Persistence, _HeightmapBuffer,
                _CaveNoiseBuffer);
            builder.TimeMeasuredGenerate();

            // builder has completed execution, so set field
            _TerrainGenerator = builder;

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!CancellationToken.IsCancellationRequested)
            {
                ChunkTerrainBuilder builder = (ChunkTerrainBuilder)_TerrainGenerator;

                DiagnosticsController.Current.RollingNoiseRetrievalTimes.Enqueue(builder.NoiseRetrievalTimeSpan);
                DiagnosticsController.Current.RollingTerrainBuildingTimes.Enqueue(builder.TerrainGenerationTimeSpan);
            }

            return Task.CompletedTask;
        }
    }
}
