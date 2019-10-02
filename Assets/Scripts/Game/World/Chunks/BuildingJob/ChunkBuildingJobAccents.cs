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
            return;
            BlockController.Current.TryGetBlockId("water", out ushort blockIdWater);
            BlockController.Current.TryGetBlockId("sand", out ushort blockIdSand);

            Vector3 localPosition = Vector3.zero;

            for (int index = 0; index < Blocks.Length; index++)
            {
                if (Blocks[index].Id != blockIdWater)
                {
                    continue;
                }

                (localPosition.x, localPosition.y, localPosition.z) =
                    Mathv.GetIndexAs3D(index, ChunkController.Size);

                GenerateSandAroundPosition(localPosition, blockIdWater, blockIdSand);
            }
        }

        private void GenerateSandAroundPosition(Vector3 localPosition, ushort blockIdWater, ushort blockIdSand)
        {
            const int sand_radius = 3;

            for (int x = -sand_radius; x < (sand_radius + 1); x++)
            {
                for (int y = -sand_radius; y < (sand_radius + 1); y++)
                {
                    for (int z = -sand_radius; z < (sand_radius + 1); z++)
                    {
                        localPosition += new Vector3(x, y, z);
                        Vector3 globalPosition = Bounds.min + localPosition;

                        if ((globalPosition.y < 0) || (globalPosition.y > ChunkController.Size.y))
                        {
                            continue;
                        }

                        if (!Bounds.Contains(globalPosition))
                        {
                            if (!WorldController.Current.TryGetBlockAt(globalPosition, out Block queriedBlock)
                                || (queriedBlock.Id == blockIdWater)
                                || (queriedBlock.Id == blockIdSand))
                            {
                                continue;
                            }

                            WorldController.Current.PlaceBlockAt(globalPosition.FloorToInt(), blockIdSand);
                        }
                        else
                        {
                            // todo fix this 
                            int queriedIndex = localPosition.To1D(ChunkController.Size);

                            if ((Blocks[queriedIndex].Id == blockIdWater)
                                || (Blocks[queriedIndex].Id == blockIdSand))
                            {
                                continue;
                            }

                            Blocks[queriedIndex].Initialise(blockIdSand);
                        }
                    }
                }
            }
        }
    }
}
