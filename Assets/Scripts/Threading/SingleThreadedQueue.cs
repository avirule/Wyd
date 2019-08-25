#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace Threading
{
    public class SingleThreadedQueue : IThreadedQueue
    {
        protected readonly Thread ProcessingThread;
        protected readonly BlockingCollection<ThreadedItem> ProcessQueue;
        protected readonly ConcurrentDictionary<object, ThreadedItem> ProcessedItems;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;

        public bool Disposed { get; private set; }
        public bool Running { get; private set; }

        public int MillisecondWaitTimeout;

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

        public virtual void Start()
        {
            ProcessingThread.Start();
            Running = true;
        }

        public virtual void Abort()
        {
            AbortTokenSource.Cancel();
            MillisecondWaitTimeout = 0;
            Running = false;
        }

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

        protected virtual void ProcessThreadedItem(ThreadedItem threadedItem)
        {
            threadedItem.Execute();
            
            ProcessedItems.TryAdd(threadedItem.Identity, threadedItem);
        }

        /// <summary>
        ///     Adds specified ThreadedItem to queue and returns a unique identity
        /// </summary>
        /// <param name="threadedItem"></param>
        /// <returns>unique object identity</returns>
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

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            ProcessQueue?.Dispose();
            Disposed = true;
        }
    }
}