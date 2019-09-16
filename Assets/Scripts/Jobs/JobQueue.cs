#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace Jobs
{
    public enum ThreadingMode
    {
        Single = 0,
        Multi = 1
    }

    public class JobQueue
    {
        private readonly Func<ThreadingMode> _ThreadingModeReference;

        private bool _Disposed;

        protected readonly int ThreadSize;
        protected readonly Thread ProcessingThread;
        protected readonly List<JobCompletionThread> InternalThreads;
        protected readonly BlockingCollection<Job> JobWaitingQueue;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;
        protected int LastThreadIndexQueuedInto;

        private int _WaitTimeout;

        /// <summary>
        ///     Determines whether the <see cref="JobQueue" /> executes <see cref="Job" /> on
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
                if ((value <= 0) || (_WaitTimeout == value))
                {
                    return;
                }

                _WaitTimeout = value;
            }
        }

        public event EventHandler<JobFinishedEventArgs> ThreadedItemFinished;

        /// <summary>
        ///     Initializes a new instance of <see cref="JobQueue" /> class.
        /// </summary>
        /// <param name="waitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </param>
        /// <param name="threadingModeReference"></param>
        /// <param name="threadSize">Size of internal <see cref="JobCompletionThread" /> pool</param>
        public JobQueue(int waitTimeout, Func<ThreadingMode> threadingModeReference = null, int threadSize = -1)
        {
            if ((threadSize <= 0) || (threadSize > 15))
            {
                threadSize = 4;
            }

            WaitTimeout = waitTimeout;
            ThreadSize = threadSize;
            _ThreadingModeReference = threadingModeReference ?? (() => ThreadingMode.Single);
            ProcessingThread = new Thread(ProcessThreadedItems);
            InternalThreads = new List<JobCompletionThread>(threadSize);
            JobWaitingQueue = new BlockingCollection<Job>();
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
            WaitTimeout = 1;
            Running = false;
        }

        private void InitialiseWorkerThreads()
        {
            for (int i = 0; i < ThreadSize; i++)
            {
                JobCompletionThread jobCompletionThread = new JobCompletionThread(WaitTimeout, AbortToken);
                jobCompletionThread.ThreadedItemFinished += OnThreadedItemFinished;
                jobCompletionThread.Start();
                InternalThreads.Add(jobCompletionThread);
            }
        }

        private void OnThreadedItemFinished(object sender, JobFinishedEventArgs args)
        {
            ThreadedItemFinished?.Invoke(sender, args);
        }

        /// <summary>
        ///     Begins internal loop for processing <see cref="Job" />s from internal queue.
        /// </summary>
        protected virtual void ProcessThreadedItems()
        {
            while (!AbortToken.IsCancellationRequested)
            {
                try
                {
                    if (!JobWaitingQueue.TryTake(out Job threadedItem, WaitTimeout, AbortToken)
                        || (threadedItem == default))
                    {
                        continue;
                    }

                    // update threading mode if object is taken
                    if (ThreadingMode != _ThreadingModeReference())
                    {
                        ThreadingMode = _ThreadingModeReference();
                    }

                    ProcessThreadedItem(threadedItem);
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
        ///     Internally processes specified <see cref="Job" /> and adds it to the list of processed
        ///     <see cref="Job" />s.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be processed.</param>
        protected virtual async void ProcessThreadedItem(Job job)
        {
            switch (ThreadingMode)
            {
                case ThreadingMode.Single:
                    await job.Execute();

                    OnThreadedItemFinished(this, new JobFinishedEventArgs(job));
                    break;
                case ThreadingMode.Multi:
                    LastThreadIndexQueuedInto = (LastThreadIndexQueuedInto + 1) % ThreadSize;

                    InternalThreads[LastThreadIndexQueuedInto].QueueThreadedItem(job);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        ///     Adds specified <see cref="Job" /> to internal queue and returns a unique identity.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be added.</param>
        /// <returns>A unique <see cref="System.Object" /> identity.</returns>
        public virtual object QueueThreadedItem(Job job)
        {
            if (!Running)
            {
                return default;
            }

            job.Initialize(Guid.NewGuid().ToString(), AbortToken);
            JobWaitingQueue.Add(job, AbortToken);

            return job.Identity;
        }

        /// <summary>
        ///     Disposes of <see cref="JobQueue" /> instance.
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
                JobWaitingQueue?.Dispose();
            }

            _Disposed = true;
        }
    }
}
