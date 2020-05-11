#region

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace Wyd.Jobs
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

            JobQueued += (sender, args) => { Interlocked.Increment(ref _QueuedJobs); };
            JobStarted += (sender, args) =>
            {
                Interlocked.Decrement(ref _QueuedJobs);
                Interlocked.Increment(ref _ProcessingJobs);
            };
            JobFinished += (sender, args) => { Interlocked.Decrement(ref _ProcessingJobs); };

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

        /// <summary>
        ///     Queues given <see cref="AsyncInvocation" /> for execution by <see cref="AsyncJobScheduler" />.
        /// </summary>
        /// <param name="asyncJob"><see cref="AsyncJob" /> to execute.</param>
        /// <remarks>
        ///     For performance reasons, the internal execution method utilizes ConfigureAwait(false).
        /// </remarks>
        public static void QueueAsyncJob(AsyncJob asyncJob)
        {
            Debug.Assert(asyncJob.ProcessInstanced != null);

            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            OnJobQueued(asyncJob);

            Task.Run(() => ExecuteJob(asyncJob));
        }

        /// <summary>
        ///     Queues given <see cref="AsyncInvocation" /> for execution by <see cref="AsyncJobScheduler" />.
        /// </summary>
        /// <param name="asyncInvocation"><see cref="AsyncInvocation" /> to invoke.</param>
        /// <remarks>
        ///     For performance reasons, the internal execution method utilizes ConfigureAwait(false).
        /// </remarks>
        public static void QueueAsyncInvocation(AsyncInvocation asyncInvocation)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }
            else if (asyncInvocation == null)
            {
                throw new NullReferenceException(nameof(asyncInvocation));
            }

            Task.Run(() => ExecuteInvocation(asyncInvocation));
        }

        /// <summary>
        ///     Waits asynchronously until work is ready to be done.
        /// </summary>
        public static async Task WaitAsync() => await _WorkerSemaphore.WaitAsync();

        /// <summary>
        ///     Waits asynchronously until work is ready to be done, or until timeout is reached..
        /// </summary>
        /// <param name="timeout"><see cref="TimeSpan" /> to wait until returning without successful wait.</param>
        public static async Task<bool> WaitAsync(TimeSpan timeout) => await _WorkerSemaphore.WaitAsync(timeout);

        #endregion


        #region Runtime

        private static async Task ExecuteInvocation(AsyncInvocation invocation)
        {
            Debug.Assert(invocation != null);

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
            Debug.Assert(asyncJob != null);
            Debug.Assert(asyncJob.ProcessInstanced != null);

            // observe cancellation token
            if (_AbortTokenSource.IsCancellationRequested)
            {
                return;
            }

            // if semaphore is empty, wait until it is released
            if (_WorkerSemaphore.CurrentCount == 0)
            {
                await _WorkerSemaphore.WaitAsync().ConfigureAwait(false);
            }

            OnJobStarted(asyncJob);

            await asyncJob.ProcessInstanced.ConfigureAwait(false);

            OnJobFinished(asyncJob);

            _WorkerSemaphore.Release();
        }

        #endregion


        #region Events

        public static event EventHandler<long> MaximumProcessingJobsChanged;

        /// <summary>
        ///     Called when a job is queued.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public static event EventHandler<AsyncJob> JobQueued;

        /// <summary>
        ///     Called when a job starts execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public static event EventHandler<AsyncJob> JobStarted;

        /// <summary>
        ///     Called when a job finishes execution.
        /// </summary>
        /// <remarks>This event will not necessarily happen synchronously with the main thread.</remarks>
        public static event EventHandler<AsyncJob> JobFinished;


        private static void OnMaximumProcessingJobsChanged(long newMaximumProcessingJobs)
        {
            MaximumProcessingJobsChanged?.Invoke(MaximumProcessingJobsChanged, newMaximumProcessingJobs);
        }

        private static void OnJobQueued(AsyncJob args)
        {
            JobQueued?.Invoke(JobQueued, args);
        }

        private static void OnJobStarted(AsyncJob args)
        {
            JobStarted?.Invoke(JobStarted, args);
        }

        private static void OnJobFinished(AsyncJob args)
        {
            JobFinished?.Invoke(JobFinished, args);
        }

        #endregion
    }
}
