#region

using Unity.Mathematics;
using Wyd.Controllers.World;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public static class GenerationConstants
    {
        public static readonly int[] IndexStepByNormalIndex =
        {
            1,
            ChunkController.SIZE_SQUARED,
            ChunkController.SIZE,
            -1,
            -ChunkController.SIZE_SQUARED,
            -ChunkController.SIZE
        };

        public static readonly int3[] FaceNormalByIteration =
        {
            new int3(1, 0, 0),
            new int3(0, 1, 0),
            new int3(0, 0, 1),
            new int3(-1, 0, 0),
            new int3(0, -1, 0),
            new int3(0, 0, -1),
        };

        public static readonly int[][] UVIndexAdjustments =
        {
            new[]
            {
                -1, // iterating x axis
                1,
                0,
            },
            new[]
            {
                0,
                -1, // iterating y axis
                1,
            },
            new[]
            {
                0,
                1,
                -1, // iterating z axis
            }
        };
    }
}
