using System;

namespace Wyd.Game.World
{
    [Flags]
    public enum WorldState
    {
        RequiresStateVerification,
        VerifyingState
    }
}
