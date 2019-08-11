#region

using Threading;
using UnityEngine;

#endregion

// ReSharper disable MemberCanBePrivate.Global

namespace Environment.Terrain.Generation.Noise.Perlin
{
    public class PerlinNoiseGenerator : ThreadedProcess
    {
        protected readonly float Lacunarity;
        protected readonly int Octaves;
        protected readonly float Persistence;
        protected readonly float Scale;
        protected readonly WorldSeed Seed;
        protected readonly Vector3Int Size;
        public float[][] Map;
        protected Vector3Int Offset;

        public PerlinNoiseGenerator(Vector3Int offset, Vector3Int size, WorldGenerationSettings settings) : this(offset,
            settings.Seed, size, settings.Octaves, settings.Scale, settings.Persistence, settings.Lacunarity)
        {
        }

        public PerlinNoiseGenerator(Vector3Int offset, WorldSeed seed, Vector3Int size, int octaves, float scale,
            float persistence,
            float lacunarity)
        {
            Offset = offset;
            Seed = seed;
            Size = size;
            Octaves = octaves;
            Scale = scale;
            Persistence = persistence;
            Lacunarity = lacunarity;
        }

        protected override void ThreadFunction()
        {
            Map = PerlinNoise.GenerateMap(Offset, Seed, Size, Octaves, Scale, Persistence, Lacunarity);
        }
    }
}