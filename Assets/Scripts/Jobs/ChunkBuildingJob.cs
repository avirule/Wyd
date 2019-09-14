#region

using Controllers.World;
using Game;
using Game.World.Blocks;
using Game.World.Chunks;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Jobs
{
    public class ChunkBuildingJob : Job
    {
        private static readonly ObjectCache<ChunkBuilderNoiseValues> NoiseValuesCache =
            new ObjectCache<ChunkBuilderNoiseValues>(null, null, true);

        private ChunkBuilder _Builder;
        private bool _GPUAcceleration;
        private ChunkBuilderNoiseValues _NoiseValues;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="frequency"></param>
        /// <param name="gpuAcceleration"></param>
        /// <param name="noiseValuesBuffer"></param>
        public void Set(
            Vector3 position, Block[] blocks, float frequency, bool gpuAcceleration = false,
            ComputeBuffer noiseValuesBuffer = null)
        {
            if (_Builder == default)
            {
                _Builder = new ChunkBuilder();
            }

            _Builder.AbortToken = AbortToken;
            _Builder.Rand = new Random(WorldController.Current.WorldGenerationSettings.Seed);
            _Builder.Position.Set(position.x, position.y, position.z);
            _Builder.Blocks = blocks;
            _Builder.Frequency = frequency;
            _GPUAcceleration = gpuAcceleration;

            if (noiseValuesBuffer != null)
            {
                _NoiseValues = NoiseValuesCache.RetrieveItem();
                noiseValuesBuffer.GetData(_NoiseValues.NoiseValues);
                noiseValuesBuffer.Release();
            }
        }

        protected override void Process()
        {
            if (_GPUAcceleration && (_NoiseValues != default))
            {
                _Builder.ProcessPreGeneratedNoiseData(_NoiseValues.NoiseValues);
            }
            else
            {
                _Builder.GenerateCPUBound();
            }

            _Builder.TerrainPass1();
        }

        protected override void ProcessFinished()
        {
            NoiseValuesCache.CacheItem(ref _NoiseValues);
        }
    }
}
