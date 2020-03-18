using System;

namespace Wyd.System
{
    public static class GenerationData
    {
        [Flags]
        public enum GenerationStep : ushort
        {
            RawTerrain = 0b0000_0000_0000_0001,
            Accents = 0b0000_0000_0000_0011,
            Meshing = 0b0000_0001_1111_1111,
            Complete = 0b1111_1111_1111_1111
        }

        public enum MeshingState
        {
            Unmeshed,
            UpdateRequested,
            PendingGeneration,
            MeshPending,
            Meshed
        }

        public const GenerationStep INITIAL_STEP = GenerationStep.RawTerrain;
        public const GenerationStep FINAL_TERRAIN_STEP = GenerationStep.Accents;
        public const GenerationStep FINAL_STEP = GenerationStep.Meshing;
    }
}
