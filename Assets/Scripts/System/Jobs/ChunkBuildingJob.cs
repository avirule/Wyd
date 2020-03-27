#region

using UnityEngine;
using Wyd.Game.World.Chunks;
using Wyd.System.Collections;

#endregion

namespace Wyd.System.Jobs
{
    public class ChunkBuildingJob : Job
    {
        private static readonly ObjectCache<ChunkRawTerrainBuilder> _ChunkRawTerrainBuilders =
            new ObjectCache<ChunkRawTerrainBuilder>();

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

        public override void PreProcess()
        {
            _TerrainBuilder = _ChunkRawTerrainBuilders.Retrieve() ?? new ChunkRawTerrainBuilder();
            _TerrainBuilder.SetData(_GenerationData, _Frequency, _Persistence, _GpuAcceleration, _NoiseValuesBuffer);
        }

        protected override void Process()
        {
            _TerrainBuilder.Generate();
        }

        protected override void ProcessFinished()
        {
            // cache builder
            _ChunkRawTerrainBuilders.CacheItem(ref _TerrainBuilder);

            // clear reference
            _TerrainBuilder = null;
        }
    }
}
