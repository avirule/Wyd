using Static;
using UnityEngine;

namespace Environment.Terrain.Generation.Noise
{
    public class NoiseMap
    {
        public BoundsInt Bounds;
        public float[][] Map;
        public bool Ready;

        public NoiseMap(float[][] map, Vector3Int center, Vector3Int size, bool createArray = false)
        {
            Map = map;
            Bounds = new BoundsInt(center - new Vector3Int(size.x / 2, 0, size.z / 2), size);

            Ready = false;

            if (!createArray)
            {
                return;
            }

            StaticMethods.CreateArray(ref Map, size);
        }
    }
}