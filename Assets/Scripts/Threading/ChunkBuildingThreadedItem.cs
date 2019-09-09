#region

using Controllers.World;
using Game.World.Blocks;
using Game.World.Chunks;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Threading
{
    public class ChunkBuildingThreadedItem : ThreadedItem
    {
        private ChunkBuilder _Builder;
        private bool _MemoryNegligent;
        private float[] _NoiseValues;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="memoryNegligent"></param>
        /// <param name="noiseValues"></param>
        public void Set(Vector3 position, Block[] blocks, bool memoryNegligent = false, float[] noiseValues = null)
        {
            if (_Builder == default)
            {
                _Builder = new ChunkBuilder();
            }

            _Builder.AbortToken = AbortToken;
            _Builder.Rand = new Random(WorldController.Current.WorldGenerationSettings.Seed);
            _Builder.Position.Set(position.x, position.y, position.z);
            _Builder.Blocks = blocks;
            _MemoryNegligent = memoryNegligent;
            _NoiseValues = noiseValues;
        }

        protected override void Process()
        {
            if (_MemoryNegligent)
            {
                _Builder.ProcessPreGeneratedNoiseData(_NoiseValues);
            }
            else
            {
                _Builder.GenerateMemorySensitive();
            }
        }
    }
}
