#region

using UnityEngine;

#endregion

namespace Environment.Terrain.Generation.Noise.Perlin
{
    public static class PerlinNoise
    {
        public static float[][] GenerateMap(Vector3Int offset, Vector3Int size,
            WorldGenerationSettings generationSettings)
        {
            return GenerateMap(offset, generationSettings.Seed, size, generationSettings.Octaves,
                generationSettings.Scale,
                generationSettings.Persistence, generationSettings.Lacunarity);
        }


        private static float maxNoiseHeight = 3;
        private static float minNoiseHeight = -3;

        /// <summary>
        ///     Generates a perlin noise map
        /// </summary>
        /// <param name="offset">Offset position of map's x/z coords</param>
        /// <param name="seed"></param>
        /// <param name="size"></param>
        /// <param name="octaves"></param>
        /// <param name="scale"></param>
        /// <param name="persistence">Value between 0 and 1</param>
        /// <param name="lacunarity">Value grater than 1</param>
        /// <param name="normalize"></param>
        /// <returns></returns>
        public static float[][] GenerateMap(Vector3 offset, WorldSeed seed, Vector3 size, int octaves,
            float scale, float persistence, float lacunarity, bool normalize = false)
        {
            if (scale <= 0)
            {
                scale = 0.0001f;
            }

            float[][] noiseHeights = new float[Mathf.CeilToInt(size.x)][];

            for (int x = 0; x < size.x; x++)
            {
                noiseHeights[x] = new float[Mathf.CeilToInt(size.z)];

                for (int z = 0; z < size.z; z++)
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

                    noiseHeights[x][z] = noiseHeight;
                }
            }

            if (normalize)
            {
                for (int x = 0; x < noiseHeights.Length; x++)
                {
                    for (int z = 0; z < noiseHeights[0].Length; z++)
                    {
                        noiseHeights[x][z] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseHeights[x][z]);
                    }
                }
            }

            return noiseHeights;
        }
    }
}