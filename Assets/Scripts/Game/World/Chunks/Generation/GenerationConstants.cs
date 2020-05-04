#region

using Unity.Mathematics;
using Wyd.Controllers.World;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public static class GenerationConstants
    {
        public static readonly int[] IndexStepByTraversalNormalIndex =
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

        public static readonly Direction[] FaceDirectionByIteration =
        {
            Direction.East,
            Direction.Up,
            Direction.North,
            Direction.West,
            Direction.Down,
            Direction.South
        };

        public static readonly (int TraversalNormalAxisIndex, int3 TraversalNormal)[][] PerpendicularNormals =
        {
            // for normal (1, 0, 0)
            new[]
            {
                (1, new int3(0, 1, 0)),
                (2, new int3(0, 0, 1)),
            },
            // for normal (0, 1, 0)
            new[]
            {
                (0, new int3(1, 0, 0)),
                (2, new int3(0, 0, 1)),
            },

            // for normal (0, 0, 1)
            new[]
            {
                (0, new int3(1, 0, 0)),
                (1, new int3(0, 1, 0)),
            }
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
