#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Game.World.Chunk;

#endregion

namespace Threading
{
    public class ThreadedQueue
    {
        private readonly Func<ThreadingMode> _ThreadingModeReference;

        private bool _Disposed;

        protected readonly int ThreadSize;
        protected readonly Thread ProcessingThread;
        protected readonly List<WorkerThread> InternalThreads;
        protected readonly BlockingCollection<ThreadedItem> ItemQueue;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;
        protected int LastThreadIndexQueuedInto;

        private int _WaitTimeout;

        /// <summary>
        ///     Determines whether the <see cref="ThreadedQueue" /> executes <see cref="ThreadedItem" /> on
        ///     the
        ///     internal thread, or uses the internal thread pool.
        /// </summary>
        public ThreadingMode ThreadingMode { get; private set; }


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

        public event EventHandler<ThreadedItemFinishedEventArgs> ThreadedItemFinished;

        /// <summary>
        ///     Initializes a new instance of <see cref="ThreadedQueue" /> class.
        /// </summary>
        /// <param name="waitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </param>
        /// <param name="threadingModeReference"></param>
        /// <param name="threadSize">Size of internal <see cref="WorkerThread" /> pool</param>
        public ThreadedQueue(int waitTimeout, Func<ThreadingMode> threadingModeReference = null, int threadSize = -1)
        {
            // todo add variable that decides whether to use single-threaded or multi-threaded execution

            if ((threadSize <= 0) ||
                (threadSize > 15))
            {
                threadSize = 4;
            }

            WaitTimeout = waitTimeout;
            ThreadSize = threadSize;
            _ThreadingModeReference = threadingModeReference ?? (() => ThreadingMode.Single);
            ProcessingThread = new Thread(ProcessThreadedItems);
            InternalThreads = new List<WorkerThread>(threadSize);
            ItemQueue = new BlockingCollection<ThreadedItem>();
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
            AbortTokenSource.Cancel();

            foreach (WorkerThread workerThread in InternalThreads)
            {
                workerThread.Abort();
            }

            WaitTimeout = 1;
            Running = false;
        }

        private void InitialiseWorkerThreads()
        {
            for (int i = 0; i < ThreadSize; i++)
            {
                WorkerThread workerThread = new WorkerThread(WaitTimeout);
                workerThread.ThreadedItemFinished += OnThreadedItemFinished;
                workerThread.Start();
                InternalThreads.Add(workerThread);
            }
        }

        private void OnThreadedItemFinished(object sender, ThreadedItemFinishedEventArgs args)
        {
            ThreadedItemFinished?.Invoke(sender, args);
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
                        // update threading mode if object is taken
                        if (ThreadingMode != _ThreadingModeReference())
                        {
                            ThreadingMode = _ThreadingModeReference();
                        }

                        ProcessThreadedItem(threadedItem);
                    }
                }
                catch (OperationCanceledException)
                {
                    // thread aborted
                    return;
                }
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

                    OnThreadedItemFinished(this, new ThreadedItemFinishedEventArgs(threadedItem));
                    break;
                case ThreadingMode.Multi:
                    LastThreadIndexQueuedInto = (LastThreadIndexQueuedInto + 1) % ThreadSize;

                    InternalThreads[LastThreadIndexQueuedInto].QueueThreadedItem(threadedItem);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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

            threadedItem.Set(Guid.NewGuid(), AbortToken);
            ItemQueue.Add(threadedItem, AbortToken);

            return threadedItem.Identity;
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