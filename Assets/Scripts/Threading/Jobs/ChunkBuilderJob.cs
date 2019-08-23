#region

using Controllers.Game;
using Environment.Terrain;
using Static;
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
            GenerateRaisedStripe(index);
            //GenerateFlat(index);
            //GenerateStripedSingleLayer(index);
            //GenerateNormal(index);
        }

        private void GenerateRaisedStripe(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            int halfSize = Chunk.Size.y / 2;

            if (y > halfSize)
            {
                return;
            }

            if (((y == halfSize) && ((x % 2) == 0)) || ((y == (halfSize - 1)) && ((x % 2) != 0)))
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Grass"));
            }
            else if (y != halfSize)
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Stone"));
            }
        }

        private void GenerateFlat(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            int halfSize = Chunk.Size.y / 2;

            if (y > halfSize)
            {
                return;
            }

            if (y == halfSize)
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Grass"));
            }
            else
            {
                Blocks[index] = new Block(BlockController.Current.GetBlockId("Stone"));
            }
        }

        private void GenerateStripedSingleLayer(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            if (y != 0)
            {
                return;
            }

            if ((x % 2) == 0)
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
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

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