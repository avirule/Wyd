#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace Threading
{
    public class ThreadedQueue
    {
        private bool _Disposed;

        protected readonly Thread ProcessingThread;
        protected readonly BlockingCollection<ThreadedItem> ProcessQueue;
        protected readonly ConcurrentDictionary<object, ThreadedItem> ProcessedItems;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;

        /// <summary>
        ///     Determines whether the <see cref="Threading.ThreadedQueue" /> executes <see cref="Threading.ThreadedItem" /> on the
        ///     internal thread, or uses <see cref="System.Threading.ThreadPool" />.
        /// </summary>
        public bool MultiThreadedExecution;

        /// <summary>
        ///     Whether or not the internal thread has been started.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </summary>
        public int MillisecondWaitTimeout;

        /// <summary>
        ///     Maximum lifetime in milliseconds that a threaded item can live after finishing execution.
        /// </summary>
        public int MaximumFinishedThreadedItemLifetime;

        /// <summary>
        ///     Initializes a new instance of <see cref="Threading.ThreadedQueue" /> class.
        /// </summary>
        /// <param name="millisecondWaitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </param>
        /// <param name="maximumFinishedThreadedItemLifetime">
        ///     Maximum lifetime in milliseconds that a threaded item can live after finishing execution.
        /// </param>
        /// <param name="multiThreadedExecution">
        ///     Determines whether the <see cref="Threading.ThreadedQueue" /> executes <see cref="Threading.ThreadedItem" /> on the
        ///     internal thread, or uses <see cref="System.Threading.ThreadPool" />.
        /// </param>
        public ThreadedQueue(int millisecondWaitTimeout, int maximumFinishedThreadedItemLifetime,
            bool multiThreadedExecution = false)
        {
            // todo add variable that decides whether to use single-threaded or multi-threaded execution

            MillisecondWaitTimeout = millisecondWaitTimeout;
            MaximumFinishedThreadedItemLifetime = maximumFinishedThreadedItemLifetime;
            MultiThreadedExecution = multiThreadedExecution;

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
            if (!Running && AbortToken.IsCancellationRequested)
            {
                return;
            }

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
                    if (ProcessQueue.TryTake(out ThreadedItem threadedItem, MillisecondWaitTimeout, AbortToken) &&
                        (threadedItem != default))
                    {
                        ProcessThreadedItem(threadedItem);
                    }

                    foreach (KeyValuePair<object, ThreadedItem> kvp in ProcessedItems)
                    {
                        // todo possibly cache the value of the DateTime to compare against
                        if (kvp.Value.IsDone &&
                            (kvp.Value.ExecutionFinishTime.AddMilliseconds(MaximumFinishedThreadedItemLifetime) <
                             DateTime.Now))
                        {
                            ProcessedItems.TryRemove(kvp.Key, out ThreadedItem _);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // removed processed item that have lived for too long
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
            if (MultiThreadedExecution)
            {
                ThreadPool.QueueUserWorkItem(state => threadedItem.Execute());
            }
            else
            {
                threadedItem.Execute();
            }

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
        public virtual bool TryGetFinishedItem(object identity, out ThreadedItem threadedItem)
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

        /// <summary>
        ///     Disposes of <see cref="ThreadedQueue" /> instance.
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