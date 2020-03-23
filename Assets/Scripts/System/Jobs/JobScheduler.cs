#region

using System;
using System.Collections.Generic;
using System.Threading;
using Serilog;
using Wyd.System.Collections;

#endregion

namespace Wyd.System.Jobs
{
    public enum ThreadingMode
    {
        Single,
        Multi
    }

    public sealed class JobScheduler
    {
        private bool _Disposed;

        private readonly Thread _OperationThread;
        private readonly List<JobWorker> _Workers;
        private readonly SpinLockCollection<Job> _JobQueue;
        private readonly CancellationTokenSource _AbortTokenSource;

        private CancellationToken _AbortToken;
        private int _WorkerThreadCount;
        private TimeSpan _WaitTimeout;
        private int _ProcessingJobCount;
        private int _JobCount;
        private int _MaximumJobCount;

        /// <summary>
        ///     Determines whether the <see cref="JobScheduler" /> executes <see cref="Job" />s on
        ///     the internal thread, or uses worker threads.
        /// </summary>
        public ThreadingMode ThreadingMode { get; set; }


        /// <summary>
        ///     Whether or not the <see cref="JobScheduler" /> is currently executing incoming jobs.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        ///     Time to wait between attempts to retrieve item from internal queue.
        /// </summary>
        public TimeSpan WaitTimeout
        {
            get => _WaitTimeout;
            set
            {
                if (value < TimeSpan.Zero)
                {
                    return;
                }

                _WaitTimeout = value;
            }
        }

        /// <summary>
        ///     Total number of worker threads JobQueue is managing.
        /// </summary>
        public int WorkerThreadCount => _WorkerThreadCount;

        public int JobCount => _JobCount;
        public int ProcessingJobCount => _ProcessingJobCount;
        public int MaximumJobCount => _MaximumJobCount;


        /// <summary>
        ///     Initializes a new instance of <see cref="JobScheduler" /> class.
        /// </summary>
        /// <param name="waitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal queue.
        /// </param>
        /// <param name="threadingMode"></param>
        /// <param name="workerCount">Size of internal <see cref="JobWorker" /> pool</param>
        public JobScheduler(TimeSpan waitTimeout, ThreadingMode threadingMode,
            int workerCount = 1)
        {
            WaitTimeout = waitTimeout;
            ThreadingMode = threadingMode;
            ModifyWorkerThreadCount(workerCount);

            _OperationThread = new Thread(ProcessJobs);
            _JobQueue = new SpinLockCollection<Job>();
            _Workers = new List<JobWorker>(WorkerThreadCount);
            _AbortTokenSource = new CancellationTokenSource();
            _AbortToken = _AbortTokenSource.Token;

            Running = false;

            JobQueued += (sender, args) => Interlocked.Increment(ref _JobCount);
            JobStarted += (sender, args) => Interlocked.Increment(ref _ProcessingJobCount);
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _JobCount);
                Interlocked.Decrement(ref _ProcessingJobCount);
            };
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
            Interlocked.Exchange(ref _MaximumJobCount, _WorkerThreadCount * 10);
            OnWorkerCountChanged(this, WorkerThreadCount);
        }

        #region STATE

        /// <summary>
        ///     Begins execution of internal threaded process.
        /// </summary>
        public void Start()
        {
            Running = true;
            _OperationThread.Start();
        }

        /// <summary>
        ///     Aborts execution of internal threaded process.
        /// </summary>
        public void Abort()
        {
            _AbortTokenSource.Cancel();
            WaitTimeout = TimeSpan.Zero;
            Log.Information($"{nameof(JobScheduler)} (ID {_OperationThread.ManagedThreadId}) has safely aborted.");
        }

        #endregion

        #region JOBS

        /// <summary>
        ///     Adds specified <see cref="Job" /> to internal queue and returns a unique identity.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be added.</param>
        /// <param name="identifier">A unique <see cref="object" /> identity.</param>
        public bool TryQueueJob(Job job, out object identifier)
        {
            identifier = null;

            if (!Running
                || _AbortToken.IsCancellationRequested
                || ((MaximumJobCount > 0) && (JobCount >= MaximumJobCount)))
            {
                return false;
            }

            job.Initialize(Guid.NewGuid().ToString(), _AbortToken);
            _JobQueue.Add(job);
            OnJobQueued(this, new JobEventArgs(job));
            identifier = job.Identity;
            return true;
        }

        /// <summary>
        ///     Begins internal loop for processing <see cref="Job" />s from internal queue.
        /// </summary>
        private void ProcessJobs()
        {
            try
            {
                while (!_AbortToken.IsCancellationRequested)
                {
                    while ((_Workers.Count < WorkerThreadCount)
                           && (ThreadingMode == ThreadingMode.Multi))
                    {
                        SpawnJobWorker();
                    }

                    if (_JobQueue.TryTake(out Job job, WaitTimeout, _AbortToken))
                    {
                        ProcessJob(job);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // thread aborted
                Log.Warning($"{nameof(JobWorker)} (ID {_OperationThread.ManagedThreadId}) has critically aborted.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"{nameof(JobScheduler)} (ID {_OperationThread.ManagedThreadId}): {ex.Message}\r\n{ex.StackTrace}");
            }
            finally
            {
                Running = false;
            }
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
        ///     Internally processes specified <see cref="Job" /> and adds it
        ///     to the list of processed <see cref="Job" />s.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be processed.</param>
        private void ProcessJob(Job job)
        {
            job.PreProcess();

            switch (ThreadingMode)
            {
                case ThreadingMode.Single:
                    _Workers[0].QueueJob(job);
                    break;
                case ThreadingMode.Multi:
                    int jobWorkerIndex;

                    while (!TryGetFirstFreeWorker(out jobWorkerIndex))
                    {
                        // relinquish time slice
                        Thread.Sleep(0);
                    }

                    _Workers[jobWorkerIndex].QueueJob(job);
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
        ///     This method is used exclusively for enabling the  <see cref="T:ThreadingMode.Multi" /> mode.
        /// </remarks>
        /// <param name="jobWorkerIndex">The resultant <see cref="T:List{JobWorker}" /> index.</param>
        /// <returns>
        ///     <value>False</value> if no job is found.
        /// </returns>
        private bool TryGetFirstFreeWorker(out int jobWorkerIndex)
        {
            jobWorkerIndex = -1;

            for (int index = 0; index < _Workers.Count; index++)
            {
                if (_Workers[index].Processing)
                {
                    continue;
                }

                jobWorkerIndex = index;
                return true;
            }

            return false;
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
    }
}
