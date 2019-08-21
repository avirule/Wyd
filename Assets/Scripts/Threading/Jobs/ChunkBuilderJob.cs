#region

using System;
using Controllers.Game;
using Environment.Terrain;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

#endregion

namespace Threading.Jobs
{
    public struct ChunkBuilderJob : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<float> NoiseMap;

        public NativeArray<Block> Blocks;

        public void Execute(int index)
        {
            GenerateStripedSingleLayer(index);
            //GenerateNormal(index);
        }

        private void GenerateStripedSingleLayer(int index)
        {
            int x = index % Chunk.Size.x;
            int y = (index / Chunk.Size.x) % Chunk.Size.y;
            int z = index / (Chunk.Size.x * Chunk.Size.y);

            if (y != 0)
            {
                return;
            }

            if (x % 2 == 0)
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Stone"));
            }
            else
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Dirt"));
            }
        }

        private void GenerateNormal(int index)
        {
            int x = index % Chunk.Size.x;
            int y = (index / Chunk.Size.x) % Chunk.Size.y;
            int z = index / (Chunk.Size.x * Chunk.Size.y);
            
            int noiseIndex = x + (Chunk.Size.x * z);

            float noiseHeight = NoiseMap[noiseIndex];

            int perlinValue = Mathf.RoundToInt(noiseHeight * Chunk.Size.y);
            
            if (y > perlinValue)
            {
                return;
            }

            if ((y == perlinValue) || (y == (Chunk.Size.y - 1)))
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Grass"));
            }
            else if ((y < perlinValue) && (y > (perlinValue - 5)))
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Dirt"));
            }
            else if (y <= (perlinValue - 5))
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Stone"));
            }
        }
    }
}