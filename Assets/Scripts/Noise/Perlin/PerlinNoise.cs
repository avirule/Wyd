#region

using Game.Terrain.Generation;
using UnityEngine;

#endregion

namespace Noise.Perlin
{
    public static class PerlinNoise
    {
        public static float[][] GenerateMap(ref float[][] noiseMap, Vector3Int offset,
            WorldGenerationSettings generationSettings)
        {
            return GenerateMap(ref noiseMap, offset, generationSettings.Seed, generationSettings.Octaves,
                generationSettings.Scale,
                generationSettings.Persistence, generationSettings.Lacunarity);
        }


        private const float _MAX_NOISE_HEIGHT = 3;
        private const float _MIN_NOISE_HEIGHT = -3;

        /// <summary>
        ///     Generates a perlin noise map
        /// </summary>
        /// <param name="noiseMap"></param>
        /// <param name="offset">Offset position of map's x/z coords</param>
        /// <param name="seed"></param>
        /// <param name="size"></param>
        /// <param name="octaves"></param>
        /// <param name="scale"></param>
        /// <param name="persistence">Value between 0 and 1</param>
        /// <param name="lacunarity">Value grater than 1</param>
        /// <param name="normalize"></param>
        /// <returns>2D jagged array representing XZ noise values.</returns>
        public static float[][] GenerateMap(ref float[][] noiseMap, Vector3 offset, WorldSeed seed, int octaves,
            float scale, float persistence, float lacunarity, bool normalize = false)
        {
            if (scale <= 0)
            {
                scale = 0.0001f;
            }

            for (int x = 0; x < noiseMap.Length; x++)
            {
                for (int z = 0; z < noiseMap[0].Length; z++)
                {
                    float amplitude = 1;
                    float frequency = 1;
                    float noiseHeight = 0;

                    for (int o = 0; o < octaves; o++)
                    {
                        float sampleX = ((offset.x + x) / seed.Normalized / scale) * frequency;
                        float sampleZ = ((offset.z + z) / seed.Normalized / scale) * frequency;

                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= persistence;
                        frequency *= lacunarity;
                    }

                    noiseMap[x][z] = noiseHeight;
                }
            }

            if (normalize)
            {
                for (int x = 0; x < noiseMap.Length; x++)
                {
                    for (int z = 0; z < noiseMap[0].Length; z++)
                    {
                        noiseMap[x][z] =
                            Mathf.InverseLerp(_MIN_NOISE_HEIGHT, _MAX_NOISE_HEIGHT, noiseMap[x][z]);
                    }
                }
            }

            return noiseMap;
        }
    }
}