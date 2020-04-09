#region

using System;

#endregion

namespace Wyd.Game.World.Chunks
{
    [Flags]
    public enum TerrainStep : byte
    {
        RawTerrain = 0b0000_0001,
        AwaitingRawTerrain = 0b0000_0011,
        Complete = 0b1111_1111
    }
}
