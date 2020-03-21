#region

using UnityEngine;
using Wyd.Game.World.Chunks;
using Wyd.System.Collections;

#endregion

namespace Wyd.System.Jobs
{
    public class ChunkBuildingJob : Job
    {
        private readonly ObjectCache<ChunkRawTerrainBuilder> _RawTerrainBuilders =
            new ObjectCache<ChunkRawTerrainBuilder>(true);

        private readonly GenerationData _GenerationData;
        private readonly ComputeBuffer _NoiseValuesBuffer;
        private readonly float _Frequency;
        private readonly float _Persistence;
        private readonly bool _GpuAcceleration;

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
            ChunkRawTerrainBuilder rawTerrainBuilder =
                _RawTerrainBuilders.RetrieveItem() ?? new ChunkRawTerrainBuilder();

            rawTerrainBuilder.SetData(_GenerationData, _NoiseValuesBuffer, _Frequency, _Persistence, _GpuAcceleration);
            rawTerrainBuilder.Generate();
            _RawTerrainBuilders.CacheItem(ref rawTerrainBuilder);
        }

        protected override void ProcessFinished() { }
    }
}
