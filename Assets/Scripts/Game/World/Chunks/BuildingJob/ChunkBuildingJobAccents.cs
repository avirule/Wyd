#region

using UnityEngine;

#endregion

namespace Wyd.Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJobAccents : ChunkBuildingJob
    {
        // todo fix this...
        protected override void Process()
        {
            Generate();
        }

        private void Generate()
        {
            // todo fix this
//            BlockController.Current.TryGetBlockId("water", out ushort blockIdWater);
//            BlockController.Current.TryGetBlockId("sand", out ushort blockIdSand);
//
//            Vector3 localPosition = Vector3.zero;
//
//            for (int index = 0; index < _Blocks.Length; index++)
//            {
//                if (_Blocks[index].Id != blockIdWater)
//                {
//                    continue;
//                }
//
//                (localPosition.x, localPosition.y, localPosition.z) =
//                    Mathv.GetIndexAs3D(index, ChunkController.Size);
//
//                GenerateSandAroundPosition(localPosition, blockIdWater, blockIdSand);
//            }
        }

        private void GenerateSandAroundPosition(Vector3 localPosition, ushort blockIdWater, ushort blockIdSand)
        {
            const int sand_radius = 3;

            // todo fix this
//            for (int x = -sand_radius; x < (sand_radius + 1); x++)
//            {
//                for (int y = -sand_radius; y < (sand_radius + 1); y++)
//                {
//                    for (int z = -sand_radius; z < (sand_radius + 1); z++)
//                    {
//                        localPosition += new Vector3(x, y, z);
//                        Vector3 globalPosition = _Bounds.min + localPosition;
//
//                        if ((globalPosition.y < 0) || (globalPosition.y > ChunkController.Size.y))
//                        {
//                            continue;
//                        }
//
//                        if (!_Bounds.Contains(globalPosition))
//                        {
//                            if (!WorldController.Current.TryGetBlockAt(globalPosition, out Block queriedBlock)
//                                || (queriedBlock.Id == blockIdWater)
//                                || (queriedBlock.Id == blockIdSand))
//                            {
//                                continue;
//                            }
//
//                            WorldController.Current.TryPlaceBlockAt(globalPosition, blockIdSand);
//                        }
//                        else
//                        {
//                            // todo fix this 
//                            int queriedIndex = localPosition.To1D(ChunkController.Size);
//
//                            if ((_Blocks[queriedIndex].Id == blockIdWater)
//                                || (_Blocks[queriedIndex].Id == blockIdSand))
//                            {
//                                continue;
//                            }
//
//                            _Blocks[queriedIndex].Initialise(blockIdSand);
//                        }
//                    }
//                }
//            }
        }
    }
}
