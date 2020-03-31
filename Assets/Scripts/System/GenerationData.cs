#region

using System;
using UnityEngine;
using Wyd.System.Collections;

#endregion

namespace Wyd.System
{
    public class GenerationData
    {
        [Flags]
        public enum GenerationStep : ushort
        {
            RawTerrain = 0b0000_0000_0000_0000,
            Complete = 0b1111_1111_1111_1111
        }

        public enum MeshingState
        {
            Unmeshed,
            UpdateRequested,
            PendingGeneration,
            Meshed
        }

        public const GenerationStep INITIAL_TERRAIN_STEP = GenerationStep.RawTerrain;
        public const GenerationStep FINAL_TERRAIN_STEP = GenerationStep.RawTerrain;

        public Bounds Bounds { get; private set; }
        public Octree<ushort> Blocks { get; private set; }

        public GenerationData(Bounds bounds, Octree<ushort> blocks) => (Bounds, Blocks) = (bounds, blocks);
    }
}
