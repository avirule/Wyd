#region

using Unity.Mathematics;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public static class GenerationConstants
    {
        public const float FREQUENCY = 0.0075f;
        public const float PERSISTENCE = 0.6f;

        public const int CHUNK_SIZE_BIT_SHIFT = 6;
        public const int CHUNK_SIZE_BIT_MASK = (1 << CHUNK_SIZE_BIT_SHIFT) - 1;

        public const int CHUNK_SIZE = 1 << (CHUNK_SIZE_BIT_SHIFT - 1);
        public const int CHUNK_SIZE_SQUARED = CHUNK_SIZE * CHUNK_SIZE;
        public const int CHUNK_SIZE_CUBED = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;


        // '8' is the 'numthreads[]' value in the compute shader
        public const int CHUNK_THREAD_GROUP_SIZE = CHUNK_SIZE / 8;

        public static readonly int[] IndexStepByNormalIndex =
        {
            1,
            CHUNK_SIZE_SQUARED,
            CHUNK_SIZE,
            -1,
            -CHUNK_SIZE_SQUARED,
            -CHUNK_SIZE,
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

        public static readonly int[] NormalInt32ByIteration =
        {
            0b11_01_01_000000_000000_000000,
            0b01_11_01_000000_000000_000000,
            0b01_01_11_000000_000000_000000,
            0b00_01_01_000000_000000_000000,
            0b01_00_01_000000_000000_000000,
            0b01_01_00_000000_000000_000000,
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
