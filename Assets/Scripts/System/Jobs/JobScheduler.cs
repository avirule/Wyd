#region

using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly SpinLockCollection<JobWorker> _FreeJobWorkers;
        private readonly CancellationTokenSource _AbortTokenSource;

        private CancellationToken _AbortToken;
        private TimeSpan _WaitTimeout;
        private int _WorkerThreadCount;
        private long _JobCount;
        private long _DelegatedJobCount;
        private long _ProcessingJobCount;

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

        public int WorkerThreadCount => _WorkerThreadCount;
        public long JobCount => Interlocked.Read(ref _JobCount);
        public long DelegatedJobCount => Interlocked.Read(ref _DelegatedJobCount);
        public long ProcessingJobCount => Interlocked.Read(ref _ProcessingJobCount);


        /// <summary>
        ///     Initializes a new instance of <see cref="JobScheduler" /> class.
        /// </summary>
        /// <param name="waitTimeout">
        ///     Time in milliseconds to wait between attempts to process an item in internal queue.
        /// </param>
        /// <param name="threadingMode"></param>
        /// <param name="workerCount">Size of internal <see cref="JobWorker" /> pool</param>
        public JobScheduler(TimeSpan waitTimeout, ThreadingMode threadingMode, int workerCount = 1)
        {
            WaitTimeout = waitTimeout;
            ThreadingMode = threadingMode;
            ModifyWorkerThreadCount(workerCount);

            _OperationThread = new Thread(ExecuteScheduler);
            _JobQueue = new SpinLockCollection<Job>();
            _Workers = new List<JobWorker>(WorkerThreadCount);
            _AbortTokenSource = new CancellationTokenSource();
            _AbortToken = _AbortTokenSource.Token;
            _FreeJobWorkers = new SpinLockCollection<JobWorker>();

            Running = false;

            JobQueued += (sender, args) => Interlocked.Increment(ref _JobCount);
            JobDelegated += (sender, args) => Interlocked.Increment(ref _DelegatedJobCount);
            JobStarted += (sender, args) => Interlocked.Increment(ref _ProcessingJobCount);
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _JobCount);
                Interlocked.Decrement(ref _DelegatedJobCount);
                Interlocked.Decrement(ref _ProcessingJobCount);
            };
        }

        /// <summary>
        ///     Adds specified <see cref="Job" /> to internal queue and returns a unique identity.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be added.</param>
        /// <param name="identifier">A unique <see cref="object" /> identity.</param>
        public bool TryQueueJob(Job job, out object identifier)
        {
            identifier = null;

            if (!Running || _AbortToken.IsCancellationRequested)
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
        ///     Modifies total number of available worker threads for JobQueue.
        /// </summary>
        /// <remarks>
        ///     This separate-method approach is taken to make intent clear, and to
        ///     more idiomatically constrain the total to a positive value.
        /// </remarks>
        /// <param name="newTotal"></param>
        public void ModifyWorkerThreadCount(int newTotal)
        {
            Interlocked.Exchange(ref _WorkerThreadCount, Math.Max(newTotal, 1));
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
        }

        private void AttemptSafeAbort()
        {
            if (CheckAllJobWorkersAborted())
            {
                Log.Information($"{nameof(JobScheduler)} (ID {_OperationThread.ManagedThreadId}) has safely aborted.");
            }
            else
            {
                Log.Warning($"{nameof(JobScheduler)} (ID {_OperationThread.ManagedThreadId}) failed to safely abort.");
            }
        }

        private bool CheckAllJobWorkersAborted()
        {
            bool safeAbort = true;
            DateTime maximumWorkerLifetime = DateTime.UtcNow + TimeSpan.FromSeconds(2);

            foreach (JobWorker jobWorker in _Workers)
            {
                while (!jobWorker.InternalThread.ThreadState.HasFlag(ThreadState.Aborted))
                {
                    if (DateTime.UtcNow <= maximumWorkerLifetime)
                    {
                        Thread.Sleep(0);
                    }
                    else
                    {
                        jobWorker.ForceAbort(true);
                        safeAbort = false;
                    }

                    jobWorker.InternalThread.Join();
                }
            }

            return safeAbort;
        }

        #endregion

        #region RUNTIME

        /// <summary>
        ///     Begins internal loop for processing <see cref="Job" />s from internal queue.
        /// </summary>
        private void ExecuteScheduler()
        {
            try
            {
                while (!_AbortToken.IsCancellationRequested)
                {
                    while ((_Workers.Count < WorkerThreadCount) && (ThreadingMode == ThreadingMode.Multi))
                    {
                        SpawnJobWorker();
                    }

                    if (_JobQueue.TryTake(out Job job, WaitTimeout, _AbortToken))
                    {
                        DelegateJob(job);
                    }
                }

                AttemptSafeAbort();
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
            jobWorker.JobFinished += (sender, args) =>
            {
                _FreeJobWorkers.Add(jobWorker);
                OnJobFinished(sender, args);
            };
            jobWorker.Start();
            _Workers.Add(jobWorker);
            _FreeJobWorkers.Add(jobWorker);
        }

        /// <summary>
        ///     Internally processes specified <see cref="Job" /> and adds it
        ///     to the list of processed <see cref="Job" />s.
        /// </summary>
        /// <param name="job"><see cref="Job" /> to be processed.</param>
        private void DelegateJob(Job job)
        {
            switch (ThreadingMode)
            {
                case ThreadingMode.Single:
                    job.Execute();
                    break;
                case ThreadingMode.Multi:
                    DelegateJobByWorkload(job);
                    //DelegateJobToFreeWorker(job);

                    OnJobDelegated(this, new JobEventArgs(job));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DelegateJobByWorkload(Job job)
        {
            int index = GetLowestWorkloadJobWorkerIndex();

            _Workers[index].QueueJob(job);
        }

        private void DelegateJobToFreeWorker(Job job)
        {
            JobWorker jobWorker = _FreeJobWorkers.Take();
            jobWorker.QueueJob(job);
        }

        private int GetLowestWorkloadJobWorkerIndex()
        {
            int jobWorkerIndex = 0;
            long lowestWorkload = int.MaxValue;

            for (int index = 0; index < _Workers.Count; index++)
            {
                long jobsQueued = _Workers[index].JobsQueued;

                if (jobsQueued < lowestWorkload)
                {
                    jobWorkerIndex = index;
                    lowestWorkload = jobsQueued;
                }
            }

            return jobWorkerIndex;
        }

        /// <summary>
        ///     Attempts to return the first <see cref="JobWorker" /> that has
        ///     its `Processing` flag set to false.
        /// </summary>
        /// <remarks>
        ///     This method is used exclusively for enabling the  <see cref="T:ThreadingMode.Multi" /> mode.
        /// </remarks>
        /// <param name="jobWorker">The resultant <see cref="T:List{JobWorker}" />.</param>
        /// <returns>
        ///     <value>False</value>
        ///     if no job is found.
        /// </returns>
        private bool TryGetFirstFreeWorker(out JobWorker jobWorker)
        {
            jobWorker = _Workers.FirstOrDefault(worker => !worker.Executing && worker.JobsQueued == 0);
            return jobWorker != null;
        }

        #endregion

        #region EVENTS

        /// <summary>
        ///     Called when a job is queued.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public event JobEventHandler JobQueued;

        public event JobEventHandler JobDelegated;

        /// <summary>
        ///     Called when a job starts execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public event JobEventHandler JobStarted;

        /// <summary>
        ///     Called when a job finishes execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public event JobEventHandler JobFinished;

        public event EventHandler<int> WorkerCountChanged;

        private void OnJobQueued(object sender, JobEventArgs args)
        {
            JobQueued?.Invoke(sender, args);
        }

        private void OnJobDelegated(object sender, JobEventArgs args)
        {
            JobDelegated?.Invoke(sender, args);
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
