#region

using Environment.Terrain.Generation;
using Logging;
using NLog;
using Noise.Perlin;
using Static;
using UnityEngine;

#endregion

namespace Noise
{
    public class NoiseMap
    {
        public BoundsInt Bounds;
        public float[][] Map;
        public bool Ready;

        public NoiseMap(float[][] map, Vector3Int center, Vector3Int size)
        {
            Map = map;
            Bounds = new BoundsInt(center - new Vector3Int(size.x / 2, 0, size.z / 2), size);

            Ready = false;
        }

        public void Generate(Vector3Int offset, Vector3Int size, WorldGenerationSettings worldGenerationSettings)
        {
            Ready = false;

            ChangeCenter(offset, size);
            Map = PerlinNoise.GenerateMap(offset, size, worldGenerationSettings);

            Ready = true;
        }

        private void ChangeCenter(Vector3Int center, Vector3Int size)
        {
            Bounds = new BoundsInt(center - new Vector3Int(size.x / 2, 0, size.z / 2), size);
        }

        public float[] GetSection(Vector3Int position, Vector3Int size)
        {
            if (!Mathv.ContainsVector3Int(Bounds, position))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to retrieve noise map by offset: offset ({position.x}, {position.z}) outside of noise map.");
                return null;
            }

            Vector3Int indexes = position - Bounds.min;

            float[] noiseMap = new float[size.x * size.z];

            for (int index = 0; index < noiseMap.Length; index++)
            {
                int x = index / (size.y * size.z);
                int z = index % size.z;

                noiseMap[index] = Map[indexes.x + x][indexes.z + z];
            }

            return noiseMap;
        }
    }
}