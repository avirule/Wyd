#region

using Unity.Mathematics;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public static class GenerationConstants
    {
        public const float FREQUENCY = 0.0075f;
        public const float PERSISTENCE = 0.6f;

        public const int CHUNK_SIZE = 32;
        public const int CHUNK_SIZE_SQUARED = CHUNK_SIZE * CHUNK_SIZE;
        public const int CHUNK_SIZE_SQUARED_HALF = CHUNK_SIZE_SQUARED / 2;
        public const int CHUNK_SIZE_CUBED = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;
        public const int CHUNK_SIZE_CUBED_HALF = CHUNK_SIZE_CUBED / 2;
        public const int CHUNK_SIZE_MINUS_ONE = CHUNK_SIZE - 1;

        // '8' is the 'numthreads[]' value in the compute shader
        public const int CHUNK_THREAD_GROUP_SIZE = CHUNK_SIZE / 8;

        public static readonly int[] IndexStepByNormalIndex =
        {
            1,
            CHUNK_SIZE_SQUARED,
            CHUNK_SIZE,
            -1,
            -CHUNK_SIZE_SQUARED,
            -CHUNK_SIZE
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
