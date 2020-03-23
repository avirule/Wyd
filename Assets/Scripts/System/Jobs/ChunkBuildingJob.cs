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
            // todo execute on main thread
            _TerrainBuilder = _RawTerrainBuilders.RetrieveItem() ?? new ChunkRawTerrainBuilder();
            _TerrainBuilder.SetData(_GenerationData, _NoiseValuesBuffer, _Frequency, _Persistence, _GpuAcceleration);
        }

        protected override void Process()
        {
            _TerrainBuilder.Generate();
        }

        protected override void ProcessFinished()
        {
            // cache builder
            _RawTerrainBuilders.CacheItem(ref _TerrainBuilder);

            // clear reference
            _TerrainBuilder = null;
        }
    }
}
