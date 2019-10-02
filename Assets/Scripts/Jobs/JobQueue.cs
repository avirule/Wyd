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
        Single,
        Multi,
        Adaptive
    }

    public class JobQueue
    {
        private bool _Disposed;
        private int _WaitTimeout;

        protected readonly Thread ProcessingThread;
        protected readonly List<JobWorker> Workers;
        protected readonly BlockingCollection<Job> ProcessQueue;
        protected readonly CancellationTokenSource AbortTokenSource;
        protected CancellationToken AbortToken;
        protected int LastThreadIndexQueuedInto;

        protected int ThreadPoolSize;

        /// <summary>
        ///     Determines whether the <see cref="JobQueue" /> executes <see cref="Job" /> on
        ///     the
        ///     internal thread, or uses the internal thread pool.
        /// </summary>
        public ThreadingMode ThreadingMode { get; set; }


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
        /// <param name="threadingMode"></param>
        /// <param name="threadPoolSize">Size of internal <see cref="JobWorker" /> pool</param>
        public JobQueue(int waitTimeout, ThreadingMode threadingMode = ThreadingMode.Single, int threadPoolSize = -1)
        {
            WaitTimeout = waitTimeout;
            ThreadingMode = threadingMode;
            ModifyThreadPoolSize(threadPoolSize);
            ProcessingThread = new Thread(ProcessJobs);
            ProcessQueue = new BlockingCollection<Job>();
            Workers = new List<JobWorker>(ThreadPoolSize);
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

        private void SpawnJobWorker()
        {
            JobWorker jobWorker = new JobWorker(WaitTimeout, AbortToken);
            jobWorker.JobFinished += OnJobFinished;
            jobWorker.Start();
            Workers.Add(jobWorker);
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
                    if (!ProcessQueue.TryTake(out Job job, WaitTimeout, AbortToken)
                        || (job == default))
                    {
                        continue;
                    }

                    while ((Workers.Count < ThreadPoolSize)
                           && (ThreadingMode > ThreadingMode.Single))
                    {
                        SpawnJobWorker();
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
                    EventLog.Logger.Log(LogLevel.Warn, $"Error occurred in job queue: {ex.Message}");
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

                    if (TryGetFirstFreeWorker(out int jobWorkerIndex))
                    {
                        Workers[jobWorkerIndex].QueueJob(job);
                    }
                    else
                    {
                        LastThreadIndexQueuedInto = (LastThreadIndexQueuedInto + 1) % ThreadPoolSize;

                        Workers[LastThreadIndexQueuedInto].QueueJob(job);
                    }

                    break;
                case ThreadingMode.Adaptive:
                    if (TryGetFirstFreeWorker(out jobWorkerIndex))
                    {
                        Workers[jobWorkerIndex].QueueJob(job);
                    }
                    else
                    {
                        await job.Execute();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool TryGetFirstFreeWorker(out int jobWorkerIndex)
        {
            jobWorkerIndex = -1;

            for (int index = 0; index < Workers.Count; index++)
            {
                if (!Workers[index].Processing)
                {
                    jobWorkerIndex = index;
                    return true;
                }
            }

            return false;
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
                return null;
            }

            job.Initialize(Guid.NewGuid().ToString(), AbortToken);
            ProcessQueue.Add(job, AbortToken);

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
                ProcessQueue?.Dispose();
            }

            _Disposed = true;
        }
    }
}
