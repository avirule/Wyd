using Threading;
using UnityEngine;

namespace Environment.Terrain.Generation
{
    public class ChunkGenerator : ThreadedProcess
    {
        private readonly float[][] _NoiseMap;
        private readonly Vector3Int _Size;
        public Block[][][] Blocks;

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
            Blocks = new Block[_Size.x][][];

            for (int x = 0; x < _Size.x; x++)
            {
                Blocks[x] = new Block[_Size.y][];

                for (int y = 0; y < _Size.y; y++)
                {
                    Blocks[x][y] = new Block[_Size.z];

                    for (int z = 0; z < _Size.z; z++)
                    {
                        float noiseHeight = _NoiseMap[x][z];

                        int perlinValue = Mathf.FloorToInt(noiseHeight * _Size.y);

                        if (y > perlinValue)
                        {
                            continue;
                        }

                        if ((y == perlinValue) || (y == (Chunk.Size.y - 1)))
                        {
                            Blocks[x][y][z] = new Block(1);
                        }
                        else if ((y < perlinValue) && (y > (perlinValue - 5)))
                        {
                            Blocks[x][y][z] = new Block(2);
                        }
                        else if (y <= (perlinValue - 5))
                        {
                            Blocks[x][y][z] = new Block(3);
                        }
                    }
                }
            }
        }
    }
}