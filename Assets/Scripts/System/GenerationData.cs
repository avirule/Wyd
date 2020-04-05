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
            Noise = 1,
            NoiseWaitFrameOne = 2,
            NoiseWaitFrameTwo = 4,
            RawTerrain = 8,
            AwaitingRawTerrain = 16,
            Complete = 0xFFFF
        }

        public Bounds Bounds { get; }
        public Octree<ushort> Blocks { get; }

        public GenerationData(Bounds bounds, Octree<ushort> blocks) => (Bounds, Blocks) = (bounds, blocks);
    }
}
