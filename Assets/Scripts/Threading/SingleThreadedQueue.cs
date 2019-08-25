#region

using System;
using System.Collections.Concurrent;
using System.Threading;

#endregion

namespace Threading
{
    public class SingleThreadedQueue
    {
        private bool _Disposed;

        protected readonly Thread ProcessingThread;
        protected readonly BlockingCollection<ThreadedItem> ProcessQueue;
        protected readonly ConcurrentDictionary<object, ThreadedItem> ProcessedItems;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;

        /// <summary>
        ///     Whether or not the internal thread has been started.
        /// </summary>
        public bool Running { get; private set; }

        public int MillisecondWaitTimeout;

        /// <summary>
        ///     Initializes a new instance of <see cref="Threading.SingleThreadedQueue" /> class.
        /// </summary>
        /// <param name="millisecondWaitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </param>
        public SingleThreadedQueue(int millisecondWaitTimeout)
        {
            MillisecondWaitTimeout = millisecondWaitTimeout;
            ProcessingThread = new Thread(ProcessThreadedItems);
            ProcessQueue = new BlockingCollection<ThreadedItem>();
            ProcessedItems = new ConcurrentDictionary<object, ThreadedItem>();
            AbortTokenSource = new CancellationTokenSource();
            AbortToken = AbortTokenSource.Token;

            Running = false;
        }

        /// <summary>
        ///     Begins execution of internal threaded process.
        /// </summary>
        public virtual void Start()
        {
            ProcessingThread.Start();
            Running = true;
        }

        /// <summary>
        ///     Aborts execution of internal threaded process.
        /// </summary>
        public virtual void Abort()
        {
            AbortTokenSource.Cancel();
            MillisecondWaitTimeout = 0;
            Running = false;
        }

        /// <summary>
        ///     Begins internal loop for processing <see cref="Threading.ThreadedItem" />s from internal queue.
        /// </summary>
        protected virtual void ProcessThreadedItems()
        {
            while (!AbortToken.IsCancellationRequested)
            {
                try
                {
                    if (!ProcessQueue.TryTake(out ThreadedItem threadedItem, MillisecondWaitTimeout, AbortToken))
                    {
                        continue;
                    }

                    if (threadedItem == default)
                    {
                        continue;
                    }

                    ProcessThreadedItem(threadedItem);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            Dispose();
        }

        /// <summary>
        ///     Internally processes specified <see cref="Threading.ThreadedItem" /> and adds it to the list of processed
        ///     <see cref="Threading.ThreadedItem" />s.
        /// </summary>
        /// <param name="threadedItem"><see cref="Threading.ThreadedItem" /> to be processed.</param>
        protected virtual void ProcessThreadedItem(ThreadedItem threadedItem)
        {
            threadedItem.Execute();

            ProcessedItems.TryAdd(threadedItem.Identity, threadedItem);
        }

        /// <summary>
        ///     Adds specified <see cref="Threading.ThreadedItem" /> to internal queue and returns a unique identity.
        /// </summary>
        /// <param name="threadedItem"><see cref="Threading.ThreadedItem" /> to be added.</param>
        /// <returns>A unique <see cref="System.Object" /> identity.</returns>
        public virtual object AddThreadedItem(ThreadedItem threadedItem)
        {
            if (!Running)
            {
                return null;
            }

            string guid = Guid.NewGuid().ToString();
            threadedItem.Identity = guid;
            ProcessQueue.Add(threadedItem, AbortToken);

            return guid;
        }

        /// <summary>
        ///     Tries to get a <see cref="Threading.ThreadedItem" /> from the internal processed list.
        ///     If successful, the <see cref="Threading.ThreadedItem" /> is removed from the internal list as well.
        /// </summary>
        /// <param name="identity">
        ///     <see cref="System.Object" /> representing identity of desired
        ///     <see cref="Threading.ThreadedItem" />.
        /// </param>
        /// <param name="threadedItem"><see cref="Threading.ThreadedItem" /> found.</param>
        /// <returns>
        ///     <see langword="true" /> if <see cref="Threading.ThreadedItem" /> exists; otherwise, <see langword="false" />.
        /// </returns>
        public virtual bool TryGetFinishedItem(object identity, out ThreadedItem threadedItem)
        {
            if (!ProcessedItems.ContainsKey(identity))
            {
                threadedItem = default;
                return false;
            }

            ProcessedItems.TryRemove(identity, out threadedItem);
            return true;
        }

        /// <summary>
        ///     Disposes of <see cref="Threading.SingleThreadedQueue" /> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                ProcessQueue?.Dispose();
            }

            _Disposed = true;
        }
    }
}