#region

using System;

#endregion

namespace Wyd.Game.World.Chunks
{
    [Flags]
    public enum MeshState : byte
    {
        UpdateRequested = 1,
        Meshing = 2,
        Meshed = 4
    }
}
