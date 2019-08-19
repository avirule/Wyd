#region

using System.Threading;
using Controllers.Game;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

#endregion

namespace Environment.Terrain.Generation
{
    [BurstCompile]
    public struct ChunkBuilderJob : IJobParallelFor
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<float> NoiseMap;
        public NativeArray<Block> Blocks;
        // todo implement safe alternative for blocks with more than 6 individual faces
        public int NonAirBlocksCount;

        public ChunkBuilderJob(Block[] blocks, float[] noiseMap)
        {
            NoiseMap = new NativeArray<float>(noiseMap, Allocator.Persistent);
            Blocks = new NativeArray<Block>(blocks, Allocator.Persistent);
            NonAirBlocksCount = 0;
        }

        public void Execute(int index)
        {
            int y = 0;
            int noiseIndex = 0;

            if (index != 0)
            {
                y = (index / Chunk.Size.z) * Chunk.Size.y;
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

            Interlocked.Increment(ref NonAirBlocksCount);
        }
    }
}