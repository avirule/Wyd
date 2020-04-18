#region

using System;

#endregion

namespace Wyd.Game.World
{
    [Flags]
    public enum WorldState : byte
    {
        RequiresStateVerification = 0b0001,
        VerifyingState = 0b0010
    }
}
