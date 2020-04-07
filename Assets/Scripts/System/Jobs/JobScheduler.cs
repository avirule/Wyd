#region

using System;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly CancellationTokenSource _AbortTokenSource;
        private readonly AsyncCollection<Job> _JobQueue;

        private CancellationToken _AbortToken;
        private int _WorkerThreadCount;


        private long _JobsQueued;
        private long _DelegatedJobCount;
        private long _ProcessingJobCount;

        public int WorkerThreadCount => _WorkerThreadCount;
        public long JobsQueued => Interlocked.Read(ref _JobsQueued);
        public long ProcessingJobCount => Interlocked.Read(ref _ProcessingJobCount);


        /// <summary>
        ///     Initializes a new instance of <see cref="JobScheduler" /> class.
        /// </summary>
        public JobScheduler(int workerCount = 1)
        {
            ModifyWorkerThreadCount(workerCount);

            _JobQueue = new AsyncCollection<Job>();
            _AbortTokenSource = new CancellationTokenSource();
            _AbortToken = _AbortTokenSource.Token;

            JobQueued += (sender, args) => Interlocked.Increment(ref _JobsQueued);
            JobStarted += (sender, args) => Interlocked.Increment(ref _ProcessingJobCount);
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _JobsQueued);
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

            if (_AbortToken.IsCancellationRequested)
            {
                return false;
            }

            job.Initialize(Guid.NewGuid().ToString(), _AbortToken);
            Task.Run(() => _JobQueue.PushAsync(job, _AbortToken), _AbortToken);
            OnJobQueued(this, new JobEventArgs(job));
            identifier = job.Identity;
            return true;
        }


        #region STATE

        /// <summary>
        ///     Begins execution of internal threaded process.
        /// </summary>
        public void SpawnWorkers()
        {
            for (int i = 0; i < WorkerThreadCount; i++)
            {
                Task.Run(ProcessItemQueue, _AbortToken);
            }
        }

        /// <summary>
        ///     Aborts execution of internal threaded process.
        /// </summary>
        public void Abort()
        {
            _AbortTokenSource.Cancel();
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

        #endregion


        #region RUNTIME

        private async Task ProcessItemQueue()
        {
            try
            {
                while (!_AbortToken.IsCancellationRequested)
                {
                    ExecuteJob(await _JobQueue.TakeAsync(_AbortToken));
                    Interlocked.Decrement(ref _JobsQueued);
                }
            }
            catch (OperationCanceledException)
            {
                // Thread aborted
                Log.Warning($"{nameof(JobScheduler)} has critically aborted.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {nameof(JobScheduler)}: {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private void ExecuteJob(Job job)
        {
            OnJobStarted(this, new JobEventArgs(job));
            job.Execute();
            OnJobFinished(this, new JobEventArgs(job));
        }

        #endregion


        #region EVENTS

        /// <summary>
        ///     Called when a job is queued.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public event JobEventHandler JobQueued;

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
