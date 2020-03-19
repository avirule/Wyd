#region

using System;
using System.Collections.Generic;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Compression;
using Wyd.System.Extensions;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJob : Job
    {
        protected static readonly ObjectCache<ChunkBuilderNoiseValues> NoiseValuesCache =
            new ObjectCache<ChunkBuilderNoiseValues>(true);

        protected Random _Rand;
        protected GenerationData _GenerationData;

        /// <summary>
        ///     Prepares job for new execution.
        /// </summary>
        public void SetGenerationData(GenerationData generationData)
        {
            _Rand = new Random(WorldController.Current.Seed);
            _GenerationData = generationData;
        }

        protected bool IdExistsAboveWithinRange(int startIndex, int maxSteps, ushort soughtId)
        {
            for (int i = 1; i < (maxSteps + 1); i++)
            {
                int currentIndex = startIndex + (i * ChunkController.YIndexStep);

                if (currentIndex >= _GenerationData.Blocks.Count)
                {
                    return false;
                }

// todo fix this
//                if (_Blocks[currentIndex].Id == soughtId)
//                {
//                    return true;
//                }
            }

            return false;
        }

        protected bool IdExistsWithinRadius(int startIndex, int radius, ushort soughtId)
        {
            for (int x = -radius; x < (radius + 1); x++)
            {
                for (int y = radius; y < (radius + 1); y++)
                {
                    for (int z = -radius; z < (radius + 1); z++)
                    {
                        int index = startIndex + (x, y, z).To1D(ChunkController.Size);

                        // todo fix thsis
//                        if ((index < _Blocks.Length) && (_Blocks[index].Id == soughtId))
//                        {
//                            return true;
//                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Scans the block array and returns the highest index that is non-air
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="strideSize">Number of indexes to jump each iteration</param>
        /// <param name="maxHeight">Maximum amount of iterations to stride</param>
        /// <returns></returns>
        public static int GetTopmostBlockIndex(int startIndex, int strideSize, int maxHeight)
        {
            int highestNonAirIndex = 0;

            for (int y = 0; y < maxHeight; y++)
            {
                int currentIndex = startIndex + (y * strideSize);
// todo fix this
//                if (currentIndex >= _Blocks.Length)
//                {
//                    break;
//                }
//
//                if (_Blocks[currentIndex].Id == BlockController.Air.Id)
//                {
//                    continue;
//                }

                highestNonAirIndex = currentIndex;
            }


            return highestNonAirIndex;
        }

        protected ushort GetBlockAtPosition_LastToFirst(int position)
        {
            if (position > ChunkController.SizeProduct)
            {
                return ushort.MaxValue;
            }

            uint totalPositions = 0;
            // go backwards as building is done from top to bottom
            LinkedListNode<RLENode<ushort>> currentNode = _GenerationData.Blocks.Last;

            while ((totalPositions <= position) && (currentNode != null))
            {
                uint newTotal = currentNode.Value.RunLength + totalPositions;

                if (newTotal >= position)
                {
                    return currentNode.Value.Value;
                }

                totalPositions = newTotal;
                currentNode = currentNode.Previous;
            }

            return BlockController.AIR_ID;
        }
    }
}
