#region

using Wyd.Controllers.World.Chunk;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBuilderNoiseValues
    {
        private float[] NoiseValues { get; }

        public float this[int index]
        {
            get => NoiseValues[index];
            set => NoiseValues[index] = value;
        }

        public ChunkBuilderNoiseValues() => NoiseValues = new float[WydMath.Product(ChunkController.Size)];

        public static implicit operator float[](ChunkBuilderNoiseValues chunkBuilderNoiseValues) =>
            chunkBuilderNoiseValues.NoiseValues;
    }
}
