#region

using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBuilderNoiseValues
    {
        public float[] NoiseValues { get; set; }

        public ChunkBuilderNoiseValues() => NoiseValues = new float[ChunkController.Size.Product()];
    }
}
