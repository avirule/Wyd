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

        /// <summary>
        ///     Signals to consuming members that the executed action(s)
        ///     should be aware of processing state of internal threads
        ///     to avoid over-scheduling.
        /// </summary>
        /// <remarks>
        ///     Despite the name, this mode doesn't necessarily increase
        ///     or decrease FPS. It is purely a more efficient scheduling
        ///     model, thusly any FPS gain or loss is an unintended side-effect.
        /// </remarks>
        Adaptive
    }

    public sealed class JobQueue : IDisposable
    {
        private bool _Disposed;

        private readonly List<JobWorker> _Workers;
        private readonly BlockingCollection<Job> _ProcessQueue;
        private readonly CancellationTokenSource _AbortTokenSource;

        private CancellationToken _AbortToken;
        private int _LastThreadIndexQueuedInto;
        private int _WorkerThreadCount;
        private int _WaitTimeout;

        /// <summary>
        ///     Total number of worker threads JobQueue is managing.
        /// </summary>
        public int WorkerThreadCount => _WorkerThreadCount;

        /// <summary>
        ///     Determines whether the <see cref="JobQueue" /> executes <see cref="Job" />s on
        ///     the internal thread, or uses worker threads.
        /// </summary>
        public ThreadingMode ThreadingMode { get; set; }


        /// <summary>
        ///     Whether or not the <see cref="JobQueue" /> is currently executing incoming jobs.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        ///     Time in milliseconds to wait between attempts to retrieve item from internal queue.
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

        /// <summary>
        ///     Initializes a new instance of <see cref="JobQueue" /> class.
        /// </summary>
        /// <param name="waitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal queue.
        /// </param>
        /// <param name="threadingMode"></param>
        /// <param name="threadPoolSize">Size of internal <see cref="JobWorker" /> pool</param>
        public JobQueue(int waitTimeout, ThreadingMode threadingMode = ThreadingMode.Single, int threadPoolSize = -1)
        {
            WaitTimeout = waitTimeout;
            ThreadingMode = threadingMode;
            ModifyWorkerThreadCount(threadPoolSize);
            _ProcessQueue = new BlockingCollection<Job>();
            _Workers = new List<JobWorker>(WorkerThreadCount);
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
        ///     Modifies total number of available worker threads for JobQueue.
        /// </summary>
        /// <remarks>
        ///     This separate-method approach is takes to make intent clear, and to
        ///     more idiomatically constrain the total to a positive value.
        /// </remarks>
        /// <param name="modification"></param>
        public void ModifyWorkerThreadCount(int modification)
        {
            Interlocked.Exchange(ref _WorkerThreadCount, Math.Max(modification, 1));
            OnWorkerCountChanged(this, WorkerThreadCount);
        }

        #region STATE

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

        #endregion

        #region JOBS

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

                    while ((_Workers.Count < WorkerThreadCount)
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

        private void SpawnJobWorker()
        {
            JobWorker jobWorker = new JobWorker(WaitTimeout, _AbortToken);
            jobWorker.JobStarted += OnJobStarted;
            jobWorker.JobFinished += OnJobFinished;
            jobWorker.Start();
            _Workers.Add(jobWorker);
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
                        _LastThreadIndexQueuedInto = (_LastThreadIndexQueuedInto + 1) % WorkerThreadCount;

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

        /// <summary>
        ///     Attempts to return the first <see cref="JobWorker" /> that has
        ///     its `Processing` flag set to false.
        /// </summary>
        /// <remarks>
        ///     This method is used exclusively for enabling the  <see cref="T:ThreadingMode.Adaptive" /> mode.
        /// </remarks>
        /// <param name="jobWorkerIndex">The resultant <see cref="T:List{JobWorker}" /> index.</param>
        /// <returns>
        ///     <value>False</value>
        ///     if no job is found.
        /// </returns>
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

        #endregion

        #region EVENTS

        /// <summary>
        ///     Called when a job is queued.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public event JobQueuedEventHandler JobQueued;

        /// <summary>
        ///     Called when a job starts execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public event JobStartedEventHandler JobStarted;

        /// <summary>
        ///     Called when a job finishes execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public event JobFinishedEventHandler JobFinished;

        public event WorkerCountChangedEventHandler WorkerCountChanged;

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

        private void OnWorkerCountChanged(object sender, int newCount)
        {
            WorkerCountChanged?.Invoke(sender, newCount);
        }

        #endregion

        #region DISPOSE

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

        #endregion
    }
}
