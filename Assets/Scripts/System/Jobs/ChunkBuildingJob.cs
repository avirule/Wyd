#region

using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Game.World.Chunks;

#endregion

namespace Wyd.System.Jobs
{
    public class ChunkBuildingJob : Job
    {
        private readonly GenerationData _GenerationData;
        private readonly AsyncGPUReadbackRequest _NoiseValuesRequest;
        private readonly float _Frequency;
        private readonly float _Persistence;
        private readonly bool _GpuAcceleration;

        private ChunkRawTerrainBuilder _TerrainBuilder;

        public ChunkBuildingJob(GenerationData generationData, float frequency, float persistence,
            bool gpuAcceleration = false, AsyncGPUReadbackRequest noiseValuesRequest = default)
        {
            _GenerationData = generationData;
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesRequest = noiseValuesRequest;
        }

        protected override void Process()
        {
            ChunkRawTerrainBuilder terrainBuilder = new ChunkRawTerrainBuilder(_GenerationData, _Frequency,
                _Persistence, _GpuAcceleration, _NoiseValuesRequest);
            terrainBuilder.Generate();
        }
    }
}
