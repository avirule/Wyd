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
            int y = 0;
            int noiseIndex = 0;

            if (index != 0)
            {
                y = (index / Chunk.Size.x) % Chunk.Size.y;
                noiseIndex = index % Chunk.Size.y;
            }

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

        public void Deallocate()
        {
            try
            {
                NoiseMap.Dispose();
                Blocks.Dispose();
            }
            catch (InvalidOperationException)
            {
                // native array already deallocated
            }
        }
    }
}