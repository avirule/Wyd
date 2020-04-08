#region

using System;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World
{
    public class GenerationData
    {
        public enum GenerationStep : byte
        {
            Noise = 0b0000_0001,
            RawTerrain = 0b0000_0011,
            AwaitingRawTerrain = 0b0000_0111,
            Complete = 0b1111_1111
        }

        [Flags]
        public enum MeshState : byte
        {
            UpdateRequested = 1,
            Meshing = 2,
            Meshed = 4
        }

        public Volume Volume { get; }
        public OctreeNode<ushort> Blocks { get; }

        public GenerationData(Volume volume, OctreeNode<ushort> blocks) => (Volume, Blocks) = (volume, blocks);
    }
}
