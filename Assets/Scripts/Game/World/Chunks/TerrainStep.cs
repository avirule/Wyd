using System;

namespace Wyd.Game.World.Chunks
{
    [Flags]
    public enum TerrainStep : byte
    {
        Noise = 0b0000_0001,
        RawTerrain = 0b0000_0011,
        AwaitingRawTerrain = 0b0000_0111,
        Complete = 0b1111_1111
    }
}
