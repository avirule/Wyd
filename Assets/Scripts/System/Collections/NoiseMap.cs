#region

using Wyd.Controllers.World.Chunk;

#endregion

namespace Wyd.System.Collections
{
    public class NoiseMap
    {
        private readonly float[] _NoiseMap;

        public float this[int index]
        {
            get => _NoiseMap[index];
            set => _NoiseMap[index] = value;
        }

        public NoiseMap() => _NoiseMap = new float[ChunkController.SIZE_CUBED];

        public static implicit operator float[](NoiseMap noiseMap) =>
            noiseMap._NoiseMap;
    }
}
