#region

using System;

#endregion

namespace Threading
{
    public class ThreadedItemFinishedEventArgs : EventArgs
    {
        public readonly ThreadedItem ThreadedItem;

        public ThreadedItemFinishedEventArgs(ThreadedItem threadedItem)
        {
            ThreadedItem = threadedItem;
        }
    }
}