using UnityEngine;
using Random = System.Random;

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
        /// <returns></returns>
        public static float[][] GenerateMap(Vector3Int offset, WorldSeed seed, Vector3Int size, int octaves,
            float scale,
            float persistence,
            float lacunarity)
        {
            if (scale <= 0)
            {
                scale = 0.0001f;
            }

            float[][] noiseHeights = new float[size.x][];
            float maxNoiseHeight = float.MinValue;
            float minNoiseHeight = float.MaxValue;
            Random pseudoRandom = new Random(seed);
            Vector3[] octaveOffsets = new Vector3[octaves];

            for (int o = 0; o < octaves; o++)
            {
                float offsetX = pseudoRandom.Next(0, 257) + offset.x;
                float offsetZ = pseudoRandom.Next(0, 257) + offset.z;

                octaveOffsets[o] = new Vector3(offsetX, 0, offsetZ);
            }

            for (int x = 0; x < size.x; x++)
            {
                noiseHeights[x] = new float[size.z];

                for (int z = 0; z < size.z; z++)
                {
                    float amplitude = 1;
                    float frequency = 1;
                    float noiseHeight = 0;

                    for (int o = 0; o < octaves; o++)
                    {
                        float sampleX = ((x + octaveOffsets[o].x) / scale) * frequency;
                        float sampleZ = ((z + octaveOffsets[o].z) / scale) * frequency;

                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= persistence;
                        frequency *= lacunarity;
                    }

                    if (noiseHeight > maxNoiseHeight)
                    {
                        maxNoiseHeight = noiseHeight;
                    }
                    else if (noiseHeight < minNoiseHeight)
                    {
                        minNoiseHeight = noiseHeight;
                    }

                    noiseHeights[x][z] = noiseHeight;
                }
            }

            for (int x = 0; x < noiseHeights.Length; x++)
            {
                for (int z = 0; z < noiseHeights[0].Length; z++)
                {
                    noiseHeights[x][z] = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseHeights[x][z]);
                }
            }

            //EventLog.Logger.Log(LogLevel.Info, $"Successfully generated perlin noise map of size ({size.x}, {size.z}) with offsets ({offset.x}, {offset.z}).");

            return noiseHeights;
        }
    }
}