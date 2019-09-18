#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Logging;
using NLog;

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
        private int _WaitTimeout;

        protected readonly Thread ProcessingThread;
        protected readonly List<JobCompletionThread> InternalThreads;
        protected readonly BlockingCollection<Job> JobWaitingQueue;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;
        protected int LastThreadIndexQueuedInto;

        protected int ThreadPoolSize;

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

        public event EventHandler<JobFinishedEventArgs> JobFinished;

        /// <summary>
        ///     Initializes a new instance of <see cref="JobQueue" /> class.
        /// </summary>
        /// <param name="waitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal
        ///     queue.
        /// </param>
        /// <param name="threadingModeReference"></param>
        /// <param name="threadPoolSize">Size of internal <see cref="JobCompletionThread" /> pool</param>
        public JobQueue(int waitTimeout, Func<ThreadingMode> threadingModeReference = null, int threadPoolSize = -1)
        {
            WaitTimeout = waitTimeout;
            ModifyThreadPoolSize(threadPoolSize);
            _ThreadingModeReference = threadingModeReference ?? (() => ThreadingMode.Single);
            ProcessingThread = new Thread(ProcessJobs);
            InternalThreads = new List<JobCompletionThread>(ThreadPoolSize);
            JobWaitingQueue = new BlockingCollection<Job>();
            AbortTokenSource = new CancellationTokenSource();
            AbortToken = AbortTokenSource.Token;
            // set to -1 increment in first run of process queue
            LastThreadIndexQueuedInto = -1;

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

        private void SpawnWorkerThread()
        {
            JobCompletionThread jobCompletionThread = new JobCompletionThread(WaitTimeout, AbortToken);
            jobCompletionThread.JobFinished += OnJobFinished;
            jobCompletionThread.Start();
            InternalThreads.Add(jobCompletionThread);
        }

        private void OnJobFinished(object sender, JobFinishedEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
        }

        /// <summary>
        ///     Begins internal loop for processing <see cref="Job" />s from internal queue.
        /// </summary>
        protected virtual void ProcessJobs()
        {
            while (!AbortToken.IsCancellationRequested)
            {
                try
                {
                    if (!JobWaitingQueue.TryTake(out Job job, WaitTimeout, AbortToken)
                        || (job == default))
                    {
                        continue;
                    }

                    // update threading mode if object is taken
                    if (ThreadingMode != _ThreadingModeReference())
                    {
                        ThreadingMode = _ThreadingModeReference();
                    }

                    while (InternalThreads.Count < ThreadPoolSize)
                    {
                        SpawnWorkerThread();
                    }

                    ProcessJob(job);
                }
                catch (OperationCanceledException)
                {
                    // thread aborted
                    return;
                }
                catch (Exception ex)
                {
                    EventLog.Logger.Log(LogLevel.Warn, $"Error occurred in threading daemon: {ex.Message}");
                }
            }

            Dispose();
        }

        /// <summary>
        ///     Internally processes specified <see cref="Job" /> and adds it to the list of processed
        ///     <see cref="Job" />s.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be processed.</param>
        protected virtual async void ProcessJob(Job job)
        {
            switch (ThreadingMode)
            {
                case ThreadingMode.Single:
                    await job.Execute();

                    OnJobFinished(this, new JobFinishedEventArgs(job));
                    break;
                case ThreadingMode.Multi:
                    LastThreadIndexQueuedInto = (LastThreadIndexQueuedInto + 1) % ThreadPoolSize;

                    InternalThreads[LastThreadIndexQueuedInto].QueueJob(job);
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
        public virtual object QueueJob(Job job)
        {
            if (!Running)
            {
                return default;
            }

            job.Initialize(Guid.NewGuid().ToString(), AbortToken);
            JobWaitingQueue.Add(job, AbortToken);

            return job.Identity;
        }

        public void ModifyThreadPoolSize(int modification)
        {
            Interlocked.Exchange(ref ThreadPoolSize, Math.Max(modification, 1));
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
