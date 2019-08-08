using Threading;
using UnityEngine;

namespace Environment.Terrain.Generation
{
    public class ChunkGenerator : ThreadedProcess
    {
        private readonly float[][] _NoiseMap;
        private readonly Vector3Int _Size;
        public string[][][] Blocks;

        public ChunkGenerator(float[][] noiseMap, Vector3Int size)
        {
            if (noiseMap == null)
            {
                return;
            }

            _NoiseMap = noiseMap;
            _Size = size;
        }

        protected override void ThreadFunction()
        {
            Blocks = new string[_Size.x][][];

            for (int x = 0; x < _Size.x; x++)
            {
                Blocks[x] = new string[_Size.y][];

                for (int y = 0; y < _Size.y; y++)
                {
                    Blocks[x][y] = new string[_Size.z];

                    for (int z = 0; z < _Size.z; z++)
                    {
                        float noiseHeight = _NoiseMap[x][z];

                        int perlinValue = Mathf.FloorToInt(noiseHeight * _Size.y);

                        if (y > perlinValue)
                        {
                            Blocks[x][y][z] = string.Empty;
                        }
                        else if ((y == perlinValue) || (y == (Chunk.Size.y - 1)))
                        {
                            Blocks[x][y][z] = "Grass";
                        }
                        else if ((y < perlinValue) && (y > (perlinValue - 4)))
                        {
                            Blocks[x][y][z] = "Dirt";
                        }
                        else if (y <= (perlinValue - 4))
                        {
                            Blocks[x][y][z] = "Stone";
                        }
                    }
                }
            }
        }
    }
}