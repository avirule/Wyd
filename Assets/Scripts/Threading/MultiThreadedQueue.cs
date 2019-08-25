#region

using System.Threading;

#endregion

namespace Threading
{
    /// <summary>
    ///     Derived class of SingleThreadedQueue that utilizes the ThreadPool
    /// </summary>
    public class MultiThreadedQueue : SingleThreadedQueue
    {
        /// <summary>
        ///     Initializes a new instance of <see cref="Threading.MultiThreadedQueue" /> class.
        /// </summary>
        /// <param name="millisecondWaitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </param>
        public MultiThreadedQueue(int millisecondWaitTimeout) : base(millisecondWaitTimeout)
        {
        }

        protected override void ProcessThreadedItem(ThreadedItem threadedItem)
        {
            ThreadPool.QueueUserWorkItem(state => threadedItem.Execute());

            ProcessedItems.TryAdd(threadedItem.Identity, threadedItem);
        }

        /// <summary>
        ///     Tries to get a finished <see cref="Threading.ThreadedItem" /> from the internal processed list.
        ///     If successful, the <see cref="Threading.ThreadedItem" /> is removed from the internal list as well.
        /// </summary>
        /// <param name="identity">
        ///     <see cref="System.Object" /> representing identity of desired
        ///     <see cref="Threading.ThreadedItem" />.
        /// </param>
        /// <param name="threadedItem"><see cref="Threading.ThreadedItem" /> found done.</param>
        /// <returns>
        ///     <see langword="true" /> if <see cref="Threading.ThreadedItem" /> exists and is done executing; otherwise,
        ///     <see langword="false" />.
        /// </returns>
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