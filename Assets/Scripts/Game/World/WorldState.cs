#region

using System;

#endregion

namespace Wyd.Game.World
{
    [Flags]
    public enum WorldState
    {
        RequiresStateVerification,
        VerifyingState
    }
}
