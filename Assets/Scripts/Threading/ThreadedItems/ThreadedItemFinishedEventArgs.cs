#region

using System;

#endregion

namespace Threading.ThreadedItems
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
