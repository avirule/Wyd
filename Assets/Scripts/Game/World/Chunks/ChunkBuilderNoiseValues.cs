#region

using Wyd.Controllers.World;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBuilderNoiseValues
    {
        public float[] NoiseValues { get; set; }

        public ChunkBuilderNoiseValues() => NoiseValues = new float[ChunkController.Size.Product()];
    }
}
