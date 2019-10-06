#region

using Controllers.World;

#endregion

namespace Game.World.Chunks
{
    public class ChunkBuilderNoiseValues
    {
        public float[] NoiseValues { get; set; }

        public ChunkBuilderNoiseValues() => NoiseValues = new float[ChunkController.Size.Product()];
    }
}
