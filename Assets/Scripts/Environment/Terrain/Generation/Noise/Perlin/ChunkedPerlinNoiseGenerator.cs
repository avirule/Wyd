using Static;
using UnityEngine;

namespace Environment.Terrain.Generation.Noise.Perlin
{
    public class ChunkedPerlinNoiseGenerator : PerlinNoiseGenerator
    {
        public readonly float[][] ChunkedMap;

        public Vector3Int ChunkSize;
        protected Vector3Int InitialOffset;
        public Vector3Int SizeInChunks;

        public ChunkedPerlinNoiseGenerator(Vector3Int chunkSize, Vector3Int sizeInChunks, Vector3Int offset,
            WorldSeed seed, int octaves, float scale, float persistence, float lacunarity) : base(offset, seed,
            chunkSize, octaves, scale, persistence, lacunarity)
        {
            InitialOffset = offset;
            ChunkSize = chunkSize;
            SizeInChunks = sizeInChunks;
            StaticMethods.CreateArray(ref ChunkedMap,
                new Vector3Int(ChunkSize.x * SizeInChunks.x, 0, ChunkSize.z * SizeInChunks.z));
        }

        protected override void ThreadFunction()
        {
            for (int chunkX = 0; chunkX < SizeInChunks.x; chunkX++)
            {
                for (int chunkZ = 0; chunkZ < SizeInChunks.z; chunkZ++)
                {
                    int chunkXValue = chunkX * ChunkSize.x;
                    int chunkZValue = chunkZ * ChunkSize.z;

                    base.ThreadFunction();
                    Offset = InitialOffset + new Vector3Int(chunkXValue, 0, chunkZValue);

                    for (int x = 0; x < ChunkSize.x; x++)
                    {
                        for (int z = 0; z < ChunkSize.z; z++)
                        {
                            ChunkedMap[chunkXValue + x][chunkZValue + z] = Map[x][z];
                        }
                    }
                }
            }
        }
    }
}