#region

using System;

#endregion

namespace Wyd.Game.World.Chunks
{
    [Flags]
    public enum MeshState : byte
    {
        UpdateRequested = 0b0000_0001,
        Meshing = 0b0000_0010,
        Meshed = 0b0000_0100
    }
}
