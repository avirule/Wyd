#region

using Threading;
using UnityEngine;

#endregion

namespace Environment.Terrain.Generation
{
    public class ChunkGenerator : ThreadedProcess
    {
        private readonly float[][] _NoiseMap;
        private readonly Vector3Int _Size;
        public Block[] Blocks;

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
            Blocks = new Block[_Size.x * _Size.y * _Size.z];

            for (int x = 0; x < _Size.x; x++)
            {
                for (int y = 0; y < _Size.y; y++)
                {
                    for (int z = 0; z < _Size.z; z++)
                    {
                        int index = x + (Chunk.Size.x * (y + (Chunk.Size.y * z)));

                        float noiseHeight = _NoiseMap[x][z];

                        int perlinValue = Mathf.FloorToInt(noiseHeight * _Size.y);

                        if (y > perlinValue)
                        {
                            continue;
                        }

                        if ((y == perlinValue) || (y == (Chunk.Size.y - 1)))
                        {
                            Blocks[index] = new Block(1);
                        }
                        else if ((y < perlinValue) && (y > (perlinValue - 5)))
                        {
                            Blocks[index] = new Block(2);
                        }
                        else if (y <= (perlinValue - 5))
                        {
                            Blocks[index] = new Block(3);
                        }
                    }
                }
            }
        }
    }
}