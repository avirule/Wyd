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
        public enum GenerationStep : byte
        {
            Noise = 0b0000_0001,
            NoiseWaitFrameOne = 0b0000_0011,
            NoiseWaitFrameTwo = 0b0000_0111,
            RawTerrain = 0b0000_1111,
            AwaitingRawTerrain = 0b0001_1111,
            Complete = 0b1111_1111
        }

        public Bounds Bounds { get; }
        public Octree<ushort> Blocks { get; }

        public GenerationData(Bounds bounds, Octree<ushort> blocks) => (Bounds, Blocks) = (bounds, blocks);
    }
}
