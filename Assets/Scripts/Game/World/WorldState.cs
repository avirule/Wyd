#region

using System;

#endregion

namespace Wyd.Game.World
{
    [Flags]
    public enum WorldState : byte
    {
        RequiresStateVerification = 1,
        VerifyingState = 2
    }
}
