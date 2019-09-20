#region

using Controllers.State;
using Controllers.World;
using Game.World.Blocks;
using Jobs;
using Noise;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJob : Job
    {
        protected static readonly ObjectCache<ChunkBuilderNoiseValues> NoiseValuesCache =
            new ObjectCache<ChunkBuilderNoiseValues>(null, null, true);

        protected static OpenSimplex_FastNoise NoiseFunction;

        protected Random Rand;
        protected Vector3 Position;
        protected Block[] Blocks;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        public void Set(Vector3 position, Block[] blocks)
        {
            Rand = new Random(WorldController.Current.Seed);
            Position.Set(position.x, position.y, position.z);
            Blocks = blocks;
        }

        protected bool IdExistsAboveWithinRange(int startIndex, int maxSteps, ushort soughtId)
        {
            for (int i = 1; i < (maxSteps + 1); i++)
            {
                int currentIndex = startIndex + (i * ChunkController.YIndexStep);

                if (currentIndex > Blocks.Length)
                {
                    return false;
                }

                if (Blocks[currentIndex].Id == soughtId)
                {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        ///     Scans the block array and returns the highest index that is non-air
        /// </summary>
        /// <param name="blocks">Array of blocks to scan</param>
        /// <param name="startIndex"></param>
        /// <param name="strideSize">Number of indexes to jump each iteration</param>
        /// <param name="maxHeight">Maximum amount of iterations to stride</param>
        /// <returns></returns>
        public static int GetTopmostBlockIndex(Block[] blocks, int startIndex, int strideSize, int maxHeight)
        {
            int highestNonAirIndex = 0;

            for (int y = 0; y < maxHeight; y++)
            {
                int currentIndex = startIndex + (y * strideSize);

                if (currentIndex >= blocks.Length)
                {
                    break;
                }

                if (blocks[currentIndex].Id == BlockController.BLOCK_EMPTY_ID)
                {
                    continue;
                }

                highestNonAirIndex = currentIndex;
            }

            return highestNonAirIndex;
        }
    }
}
