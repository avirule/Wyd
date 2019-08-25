using System.Threading;

namespace Threading
{
    /// <summary>
    ///     Derived class of SingleThreadedQueue that utilizes the ThreadPool
    /// </summary>
    public class MultiThreadedQueue : SingleThreadedQueue
    {
        public MultiThreadedQueue(int millisecondTimeout) : base(millisecondTimeout) {}

        protected override void ProcessThreadedItem(ThreadedItem threadedItem)
        {
            ThreadPool.QueueUserWorkItem(state => threadedItem.Execute());
            
            ProcessedItems.TryAdd(threadedItem.Identity, threadedItem);
        }

        public override bool TryGetFinishedItem(object identity, out ThreadedItem threadedItem)
        {
            if (!ProcessedItems.ContainsKey(identity) ||
                !ProcessedItems[identity].IsDone)
            {
                threadedItem = default;
                return false;
            }

            ProcessedItems.TryRemove(identity, out threadedItem);
            return true;
        }
    }
}