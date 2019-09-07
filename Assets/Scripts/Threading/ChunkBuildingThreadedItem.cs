#region

using System;
using Controllers.World;
using Game.World.Blocks;
using Game.World.Chunks;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Threading
{
    public class ChunkBuildingThreadedItem : ThreadedItem, IDisposable
    {
        private ChunkBuilder _Builder;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="memoryNegligent"></param>
        /// <param name="noiseShader"></param>
        public void Set(Vector3 position, Block[] blocks, bool memoryNegligent = false,
            ComputeShader noiseShader = null)
        {
            if (_Builder == default)
            {
                _Builder = new ChunkBuilder();
            }

            _Builder.AbortToken = AbortToken;
            _Builder.Rand = new Random(WorldController.Current.WorldGenerationSettings.Seed);
            _Builder.Position.Set(position.x, position.y, position.z);
            _Builder.Blocks = blocks;
            _Builder.MemoryNegligent = memoryNegligent;
            _Builder.NoiseShader = noiseShader;
        }

        protected override void Process()
        {
            if (_Builder.MemoryNegligent)
            {
                _Builder.GenerateMemoryNegligent();
            }

            // separate blocks to respond to errors that change the memory mode inside GenerateMemoryNegligent()
            if (!_Builder.MemoryNegligent)
            {
                _Builder.GenerateMemorySensitive();
            }
        }

        public void Dispose()
        {
            _Builder?.Dispose();
        }
    }
}