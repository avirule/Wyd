using Unity.Mathematics;
using UnityEngine.Experimental.AI;
using Wyd.Controllers.World.Chunk;

namespace Wyd.Game.World.Chunks
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
    }
}
