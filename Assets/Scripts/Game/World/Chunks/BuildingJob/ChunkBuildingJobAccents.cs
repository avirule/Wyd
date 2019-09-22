#region

using Controllers.State;
using Controllers.World;
using Game.World.Blocks;
using UnityEngine;

#endregion

namespace Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJobAccents : ChunkBuildingJob
    {
        protected override void Process()
        {
            Generate();
        }

        private void Generate()
        {
            BlockController.Current.TryGetBlockId("water", out ushort blockIdWater);
            BlockController.Current.TryGetBlockId("sand", out ushort blockIdSand);

            for (int index = 0; index < Blocks.Length; index++)
            {
                if (Blocks[index].Id == blockIdWater)
                {
                    GenerateSandAroundWater(index, blockIdWater, blockIdSand);
                }
            }
        }

        private void GenerateSandAroundWater(int index, ushort blockIdWater, ushort blockIdSand)
        {
            const int sand_radius = 3;

            if (Blocks[index].Id != blockIdWater)
            {
                return;
            }

            for (int x = -sand_radius; x < (sand_radius + 1); x++)
            {
                for (int y = -sand_radius; y < (sand_radius + 1); y++)
                {
                    for (int z = -sand_radius; z < (sand_radius + 1); z++)
                    {
                        Vector3 localPosition;
                        (localPosition.x, localPosition.y, localPosition.z) =
                            Mathv.GetVector3IntIndex(index, ChunkController.Size);
                        localPosition += new Vector3(x, y, z);
                        Vector3 globalPosition = Bounds.min + localPosition;

                        if (localPosition.y < 0)
                        {
                            continue;
                        }

                        if (!Bounds.Contains(globalPosition))
                        {
                            if (!WorldController.Current.TryGetBlockAt(globalPosition, out Block queriedBlock)
                                || (queriedBlock.Id == BlockController.BLOCK_EMPTY_ID)
                                || (queriedBlock.Id == blockIdWater)
                                || (queriedBlock.Id == blockIdSand))
                            {
                                continue;
                            }

                            WorldController.Current.TryPlaceBlockAt(globalPosition, blockIdSand);
                        }
                        else
                        {
                            // todo fix this 
                            int queriedIndex = localPosition.To1D(ChunkController.Size);
                            ushort queriedId = Blocks[queriedIndex].Id;

                            if ((queriedId == BlockController.BLOCK_EMPTY_ID)
                                || (queriedId == blockIdWater)
                                || (queriedId == blockIdSand))
                            {
                                continue;
                            }

                            Blocks[queriedId].Initialise(blockIdSand);
                        }
                    }
                }
            }
        }
    }
}
