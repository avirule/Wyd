#region

using System;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Jobs
{
    public delegate Task AsyncInvocation();

    public static class AsyncJobScheduler
    {
        private static CancellationTokenSource _AbortTokenSource;
        private static SemaphoreSlim _WorkerSemaphore;
        private static long _MaximumProcessingJobs;
        private static long _QueuedJobs;
        private static long _ProcessingJobs;

        public static CancellationToken AbortToken => _AbortTokenSource.Token;
        public static long QueuedJobs => Interlocked.Read(ref _QueuedJobs);
        public static long ProcessingJobs => Interlocked.Read(ref _ProcessingJobs);

        public static long MaximumProcessingJobs
        {
            get => Interlocked.Read(ref _MaximumProcessingJobs);
            set => Interlocked.Exchange(ref _MaximumProcessingJobs, value);
        }

        /// <summary>
        ///     Initializes a new instance of <see cref="AsyncJobScheduler" /> class.
        /// </summary>
        static AsyncJobScheduler()
        {
            _AbortTokenSource = new CancellationTokenSource();

            JobQueued += (sender, args) =>
            {
                Interlocked.Increment(ref _QueuedJobs);

                return Task.CompletedTask;
            };
            JobStarted += (sender, args) =>
            {
                Interlocked.Decrement(ref _QueuedJobs);
                Interlocked.Increment(ref _ProcessingJobs);

                return Task.CompletedTask;
            };
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _ProcessingJobs);

                return Task.CompletedTask;
            };

            ModifyMaximumProcessingJobCount(1);
        }


        #region State

        /// <summary>
        ///     Aborts execution of job scheduler.
        /// </summary>
        public static void Abort(bool abort)
        {
            if (abort)
            {
                _AbortTokenSource.Cancel();
            }
        }

        /// <summary>
        ///     Modifies total number of jobs that can be processed at once.
        /// </summary>
        /// <remarks>
        ///     This separate-method approach is taken to make intent clear, and to
        ///     more idiomatically constrain the total to a positive value.
        ///     Additionally, modifying this value causes an abrupt cancel on all active workers.
        ///     Therefore, it's advised to not use it unless absolutely necessary.
        /// </remarks>
        /// <param name="newWorkerCount">Modified count of workers for <see cref="AsyncJobScheduler" /> to initialize.</param>
        public static void ModifyMaximumProcessingJobCount(long newWorkerCount)
        {
            if (newWorkerCount == 0)
            {
                throw new ArgumentException("Parameter must be a positive value.", nameof(newWorkerCount));
            }

            Interlocked.Exchange(ref _MaximumProcessingJobs, newWorkerCount);

            _AbortTokenSource.Cancel();
            _AbortTokenSource = new CancellationTokenSource();

            _WorkerSemaphore = new SemaphoreSlim((int)MaximumProcessingJobs, (int)MaximumProcessingJobs);
            OnMaximumProcessingJobsChanged(MaximumProcessingJobs);
        }

        public static void QueueAsyncJob(AsyncJob asyncJob)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            OnJobQueued(new AsyncJobEventArgs(asyncJob));

            Task.Run(() => ExecuteJob(asyncJob));
        }

        public static void QueueAsyncInvocation(AsyncInvocation invocation)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }
            else if (invocation == null)
            {
                throw new NullReferenceException(nameof(invocation));
            }

            Task.Run(() => ExecuteInvocation(invocation));
        }

        public static async Task WaitAsync() => await _WorkerSemaphore.WaitAsync();
        public static async Task WaitAsync(TimeSpan timeout) => await _WorkerSemaphore.WaitAsync(timeout);
        
        #endregion


        #region Runtime

        private static async Task ExecuteInvocation(AsyncInvocation invocation)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            await _WorkerSemaphore.WaitAsync().ConfigureAwait(false);

            await invocation.Invoke().ConfigureAwait(false);

            _WorkerSemaphore.Release();
        }

        private static async Task ExecuteJob(AsyncJob asyncJob)
        {
            if (_AbortTokenSource.IsCancellationRequested)
            {
                return;
            }

            await _WorkerSemaphore.WaitAsync().ConfigureAwait(false);

            AsyncJobEventArgs args = new AsyncJobEventArgs(asyncJob);

            OnJobStarted(args);

            await asyncJob.Execute().ConfigureAwait(false);

            OnJobFinished(args);

            _WorkerSemaphore.Release();
        }

        #endregion


        #region Events

        public static event EventHandler<long> MaximumProcessingJobsChanged;

        /// <summary>
        ///     Called when a job is queued.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public static event AsyncJobEventHandler JobQueued;

        /// <summary>
        ///     Called when a job starts execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public static event AsyncJobEventHandler JobStarted;

        /// <summary>
        ///     Called when a job finishes execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public static event AsyncJobEventHandler JobFinished;


        private static void OnMaximumProcessingJobsChanged(long newMaximumProcessingJobs)
        {
            MaximumProcessingJobsChanged?.Invoke(MaximumProcessingJobsChanged, newMaximumProcessingJobs);
        }

        private static void OnJobQueued(AsyncJobEventArgs args)
        {
            JobQueued?.Invoke(JobQueued, args);
        }

        private static void OnJobStarted(AsyncJobEventArgs args)
        {
            JobStarted?.Invoke(JobStarted, args);
        }

        private static void OnJobFinished(AsyncJobEventArgs args)
        {
            JobFinished?.Invoke(JobFinished, args);
        }

        #endregion
    }
}
