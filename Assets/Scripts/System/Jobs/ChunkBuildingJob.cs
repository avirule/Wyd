#region

using UnityEngine;
using Wyd.Controllers.System;
using Wyd.Game.World.Chunks;

#endregion

namespace Wyd.System.Jobs
{
    public class ChunkBuildingJob : Job
    {
        private readonly GenerationData _GenerationData;
        private readonly ComputeBuffer _NoiseValuesBuffer;
        private readonly float _Frequency;
        private readonly float _Persistence;
        private readonly bool _GpuAcceleration;

        private ChunkRawTerrainBuilder _TerrainBuilder;

        public ChunkBuildingJob(GenerationData generationData, float frequency, float persistence,
            bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null)
        {
            _GenerationData = generationData;
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesBuffer = noiseValuesBuffer;
        }

        protected override void Process()
        {
            _TerrainBuilder = new ChunkRawTerrainBuilder(_GenerationData, _Frequency,
                _Persistence, _GpuAcceleration, _NoiseValuesBuffer);
            _TerrainBuilder.Generate();
        }

        protected override void ProcessFinished()
        {
            DiagnosticsController.Current.RollingNoiseRetrievalTimes.Enqueue(_TerrainBuilder.NoiseRetrievalTimeSpan);
            DiagnosticsController.Current.RollingTerrainGenerationTimes.Enqueue(_TerrainBuilder
                .TerrainGenerationTimeSpan);
        }
    }
}
