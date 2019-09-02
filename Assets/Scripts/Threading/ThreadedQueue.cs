#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Game.World.Chunk;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Threading
{
    public class ThreadedQueue
    {
        private bool _Disposed;

        protected readonly int ThreadSize;
        protected readonly Thread ProcessingThread;
        protected readonly List<WorkerThread> InternalThreads;
        protected readonly BlockingCollection<ThreadedItem> ItemQueue;
        protected readonly ConcurrentDictionary<object, ThreadedItem> ProcessedItems;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;
        protected int LastThreadIndexQueuedInto;

        private int _WaitTimeout;

        /// <summary>
        ///     Determines whether the <see cref="ThreadedQueue" /> executes <see cref="ThreadedItem" /> on
        ///     the
        ///     internal thread, or uses the internal thread pool.
        /// </summary>
        public ThreadingMode ThreadingMode;

        /// <summary>
        ///     Whether or not the internal thread has been started.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </summary>
        public int WaitTimeout
        {
            get => _WaitTimeout;
            set
            {
                if ((value <= 0) ||
                    (_WaitTimeout == value))
                {
                    return;
                }

                _WaitTimeout = value;
            }
        }

        /// <summary>
        ///     Initializes a new instance of <see cref="ThreadedQueue" /> class.
        /// </summary>
        /// <param name="waitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </param>
        /// <param name="threadSize">Size of internal <see cref="WorkerThread" /> pool</param>
        /// <param name="threadingMode">
        ///     Determines whether the <see cref="ThreadedQueue" /> executes <see cref="ThreadedItem" /> on the
        ///     internal thread, or uses <see cref="System.Threading.ThreadPool" />.
        /// </param>
        public ThreadedQueue(int waitTimeout, int threadSize = -1, ThreadingMode threadingMode = ThreadingMode.Single)
        {
            // todo add variable that decides whether to use single-threaded or multi-threaded execution

            if (threadSize <= 0 ||
                threadSize > 15)
            {
                // cnt / 2 assumes hyper-threading
                threadSize = SystemInfo.processorCount / 2;
            }

            WaitTimeout = waitTimeout;
            ThreadSize = threadSize;
            ThreadingMode = threadingMode;
            ProcessingThread = new Thread(ProcessThreadedItems);
            InternalThreads = new List<WorkerThread>(threadSize);
            ItemQueue = new BlockingCollection<ThreadedItem>();
            ProcessedItems = new ConcurrentDictionary<object, ThreadedItem>();
            AbortTokenSource = new CancellationTokenSource();
            AbortToken = AbortTokenSource.Token;
            LastThreadIndexQueuedInto = 0;

            InitialiseWorkerThreads();

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

            foreach (WorkerThread workerThread in InternalThreads)
            {
                workerThread.Abort();
            }

            WaitTimeout = 0;
            Running = false;
        }

        private void InitialiseWorkerThreads()
        {
            for (int i = 0; i < ThreadSize; i++)
            {
                WorkerThread workerThread = new WorkerThread(WaitTimeout);
                workerThread.FinishedItem += OnWorkerThreadFinishedItem;
                workerThread.Start();
                InternalThreads.Add(workerThread);
            }
        }

        private void OnWorkerThreadFinishedItem(object sender, WorkerThreadFinishedItemEventArgs args)
        {
            ProcessedItems.TryAdd(args.ThreadedItem.Identity, args.ThreadedItem);
        }

        /// <summary>
        ///     Begins internal loop for processing <see cref="ThreadedItem" />s from internal queue.
        /// </summary>
        protected virtual void ProcessThreadedItems()
        {
            while (!AbortToken.IsCancellationRequested)
            {
                try
                {
                    if (ItemQueue.TryTake(out ThreadedItem threadedItem, WaitTimeout, AbortToken) &&
                        (threadedItem != default))
                    {
                        ProcessThreadedItem(threadedItem);
                    }

                    foreach (KeyValuePair<object, ThreadedItem> kvp in ProcessedItems)
                    {
                        // todo possibly cache the value of the DateTime to compare against
                        if (kvp.Value.IsDone &&
                            (kvp.Value.FinishTime.AddMilliseconds(4000) <
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
        ///     Internally processes specified <see cref="ThreadedItem" /> and adds it to the list of processed
        ///     <see cref="ThreadedItem" />s.
        /// </summary>
        /// <param name="threadedItem"><see cref="ThreadedItem" /> to be processed.</param>
        protected virtual async void ProcessThreadedItem(ThreadedItem threadedItem)
        {
            switch (ThreadingMode)
            {
                case ThreadingMode.Single:
                    await threadedItem.Execute();
                    break;
                case ThreadingMode.Multi:
                    LastThreadIndexQueuedInto = (LastThreadIndexQueuedInto + 1) % ThreadSize;

                    InternalThreads[LastThreadIndexQueuedInto].QueueThreadedItem(threadedItem);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ProcessedItems.TryAdd(threadedItem.Identity, threadedItem);
        }

        /// <summary>
        ///     Adds specified <see cref="ThreadedItem" /> to internal queue and returns a unique identity.
        /// </summary>
        /// <param name="threadedItem"><see cref="ThreadedItem" /> to be added.</param>
        /// <returns>A unique <see cref="System.Object" /> identity.</returns>
        public virtual object QueueThreadedItem(ThreadedItem threadedItem)
        {
            if (!Running)
            {
                return default;
            }

            threadedItem.Identity = Guid.NewGuid().ToString();
            ItemQueue.Add(threadedItem, AbortToken);

            return threadedItem.Identity;
        }

        /// <summary>
        ///     Tries to get a finished <see cref="ThreadedItem" /> from the internal processed list.
        ///     If successful, the <see cref="ThreadedItem" /> is removed from the internal list as well.
        /// </summary>
        /// <param name="identity">
        ///     <see cref="System.Object" /> representing identity of desired
        ///     <see cref="ThreadedItem" />.
        /// </param>
        /// <param name="threadedItem"><see cref="ThreadedItem" /> found done.</param>
        /// <returns>
        ///     <see langword="true" /> if <see cref="ThreadedItem" /> exists and is done executing; otherwise,
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
                ItemQueue?.Dispose();
            }

            _Disposed = true;
        }
    }
}