#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    public sealed class JobQueue : IDisposable
    {
        private bool _Disposed;
        private int _WaitTimeout;

        private readonly List<JobWorker> _Workers;
        private readonly BlockingCollection<Job> _ProcessQueue;
        private readonly CancellationTokenSource _AbortTokenSource;
        private CancellationToken _AbortToken;
        private int _LastThreadIndexQueuedInto;

        private int _ThreadPoolSize;
        private int _JobCount;

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
        ///     Time in milliseconds to wait between failed attempts to retrieve item from internal queue.
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

        public int JobCount { get; private set; }
        public int ActiveJobCount { get; private set; }

        public event JobQueuedEventHandler JobQueued;
        public event JobStartedEventHandler JobStarted;
        public event JobFinishedEventHandler JobFinished;

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
            _ProcessQueue = new BlockingCollection<Job>();
            _Workers = new List<JobWorker>(_ThreadPoolSize);
            _AbortTokenSource = new CancellationTokenSource();
            _AbortToken = _AbortTokenSource.Token;
            // set to -1 increment in first run of process queue
            _LastThreadIndexQueuedInto = -1;

            Running = false;

            JobQueued += (sender, args) => { JobCount += 1; };
            JobStarted += (sender, args) => { ActiveJobCount += 1; };
            JobFinished += (sender, args) => { JobCount = ActiveJobCount -= 1; };
        }

        /// <summary>
        ///     Begins execution of internal threaded process.
        /// </summary>
        public void Start()
        {
            Task.Factory.StartNew(ProcessJobs, null,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach);
            Running = true;
        }

        /// <summary>
        ///     Aborts execution of internal threaded process.
        /// </summary>
        public void Abort()
        {
            _AbortTokenSource.Cancel();
            WaitTimeout = 1;
            Running = false;
        }

        private void SpawnJobWorker()
        {
            JobWorker jobWorker = new JobWorker(WaitTimeout, _AbortToken);
            jobWorker.JobStarted += OnJobStarted;
            jobWorker.JobFinished += OnJobFinished;
            jobWorker.Start();
            _Workers.Add(jobWorker);
        }

        /// <summary>
        ///     Begins internal loop for processing <see cref="Job" />s from internal queue.
        /// </summary>
        private async Task ProcessJobs(object state)
        {
            while (!_AbortToken.IsCancellationRequested)
            {
                try
                {
                    if (!_ProcessQueue.TryTake(out Job job, WaitTimeout, _AbortToken)
                        || (job == default))
                    {
                        continue;
                    }

                    while ((_Workers.Count < _ThreadPoolSize)
                           && (ThreadingMode > ThreadingMode.Single))
                    {
                        SpawnJobWorker();
                    }

                    await ProcessJob(job);
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
        private async Task ProcessJob(Job job)
        {
            switch (ThreadingMode)
            {
                case ThreadingMode.Single:
                    await ExecuteJob(job);
                    break;
                case ThreadingMode.Multi:

                    if (TryGetFirstFreeWorker(out int jobWorkerIndex))
                    {
                        _Workers[jobWorkerIndex].QueueJob(job);
                    }
                    else
                    {
                        _LastThreadIndexQueuedInto = (_LastThreadIndexQueuedInto + 1) % _ThreadPoolSize;

                        _Workers[_LastThreadIndexQueuedInto].QueueJob(job);
                    }

                    break;
                case ThreadingMode.Adaptive:
                    if (TryGetFirstFreeWorker(out jobWorkerIndex))
                    {
                        _Workers[jobWorkerIndex].QueueJob(job);
                    }
                    else
                    {
                        await ExecuteJob(job);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool TryGetFirstFreeWorker(out int jobWorkerIndex)
        {
            jobWorkerIndex = -1;

            for (int index = 0; index < _Workers.Count; index++)
            {
                if (!_Workers[index].Processing)
                {
                    jobWorkerIndex = index;
                    return true;
                }
            }

            return false;
        }

        private async Task ExecuteJob(Job job)
        {
            OnJobStarted(this, new JobEventArgs(job));
            await job.Execute();
            OnJobFinished(this, new JobEventArgs(job));
        }

        /// <summary>
        ///     Adds specified <see cref="Job" /> to internal queue and returns a unique identity.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be added.</param>
        /// <returns>A unique <see cref="System.Object" /> identity.</returns>
        public object QueueJob(Job job)
        {
            if (!Running)
            {
                return null;
            }

            job.Initialize(Guid.NewGuid().ToString(), _AbortToken);
            _ProcessQueue.Add(job, _AbortToken);
            OnJobQueued(this, new JobEventArgs(job));

            return job.Identity;
        }

        public void ModifyThreadPoolSize(int modification)
        {
            Interlocked.Exchange(ref _ThreadPoolSize, Math.Max(modification, 1));
        }

        private void OnJobQueued(object sender, JobEventArgs args)
        {
            JobQueued?.Invoke(sender, args);
        }
        
        private void OnJobStarted(object sender, JobEventArgs args)
        {
            JobStarted?.Invoke(sender, args);
        }
        
        private void OnJobFinished(object sender, JobEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
        }

        /// <summary>
        ///     Disposes of <see cref="JobQueue" /> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                _ProcessQueue?.Dispose();
            }

            _Disposed = true;
        }
    }
}
