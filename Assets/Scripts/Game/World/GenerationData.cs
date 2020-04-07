#region

using System;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World
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

        public Volume Volume { get; }
        public Octree<ushort> Blocks { get; }

        public GenerationData(Volume volume, Octree<ushort> blocks) => (Volume, Blocks) = (volume, blocks);
    }
}
