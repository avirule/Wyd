#region

using Controllers.Game;
using Threading;
using UnityEngine;

#endregion

namespace Environment.Terrain.Generation
{
    public class ChunkBuilder : ThreadedProcess
    {
        private readonly BlockController _BlockController;
        private readonly float[][] _NoiseMap;
        private readonly Vector3Int _Size;
        public Block[] Blocks;

        public ChunkBuilder(BlockController blockController, float[][] noiseMap, Vector3Int size)
        {
            if (noiseMap == null)
            {
                return;
            }

            _BlockController = blockController;
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
                        if (Die)
                        {
                            return;
                        }

                        int index = x + (Chunk.Size.x * (y + (Chunk.Size.y * z)));

                        float noiseHeight = _NoiseMap[x][z];

                        int perlinValue = Mathf.RoundToInt(noiseHeight * _Size.y);

                        if (y > perlinValue)
                        {
                            continue;
                        }

                        if ((y == perlinValue) || (y == (Chunk.Size.y - 1)))
                        {
                            Blocks[index] = new Block(_BlockController.GetBlockId("Grass"));
                        }
                        else if ((y < perlinValue) && (y > (perlinValue - 5)))
                        {
                            Blocks[index] = new Block(_BlockController.GetBlockId("Dirt"));
                        }
                        else if (y <= (perlinValue - 5))
                        {
                            Blocks[index] = new Block(_BlockController.GetBlockId("Stone"));
                        }
                    }
                }
            }
        }
    }
}