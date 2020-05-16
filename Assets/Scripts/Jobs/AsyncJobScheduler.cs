#region

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

#endregion

namespace Wyd.Jobs
{
    public delegate Task AsyncInvocation();

    public static class AsyncJobScheduler
    {
        private static readonly CancellationTokenSource _AbortTokenSource;
        private static readonly SemaphoreSlim _WorkerSemaphore;
        private static long _QueuedJobs;
        private static long _ProcessingJobs;

        /// <summary>
        ///     <see cref="CancellationToken" /> signalled whenever <see cref="Abort" /> is called.
        /// </summary>
        public static CancellationToken AbortToken => _AbortTokenSource.Token;

        /// <summary>
        ///     Number of jobs current queued.
        /// </summary>
        public static long QueuedJobsCount => Interlocked.Read(ref _QueuedJobs);

        /// <summary>
        ///     Number of jobs current being executed.
        /// </summary>
        public static long ProcessingJobsCount => Interlocked.Read(ref _ProcessingJobs);

        /// <summary>
        ///     Maximum number of jobs that are able to run concurrently.
        /// </summary>
        public static int MaximumConcurrentJobs { get; }

        /// <summary>
        ///     Initializes the static instance of the <see cref="AsyncJobScheduler" /> class.
        /// </summary>
        static AsyncJobScheduler()
        {
            // set maximum concurrent jobs to logical core count - 2
            // remark: two is subtracted from total logical core count to avoid hogging
            //     resources from the core the main thread is on, with an extra logical core as a buffer.
            //
            //     Largely, the goal here is to ensure this class remains lightweight and doesn't
            //     interfere with other critical processes.
            MaximumConcurrentJobs = Environment.ProcessorCount - 2;

            _AbortTokenSource = new CancellationTokenSource();
            _WorkerSemaphore = new SemaphoreSlim(MaximumConcurrentJobs, MaximumConcurrentJobs);

            JobQueued += (sender, args) => { Interlocked.Increment(ref _QueuedJobs); };
            JobStarted += (sender, args) =>
            {
                Interlocked.Decrement(ref _QueuedJobs);
                Interlocked.Increment(ref _ProcessingJobs);
            };
            JobFinished += (sender, args) => { Interlocked.Decrement(ref _ProcessingJobs); };
        }


        #region State

        /// <summary>
        ///     Queues given <see cref="AsyncJob" /> for execution by <see cref="AsyncJobScheduler" />.
        /// </summary>
        /// <param name="asyncJob"><see cref="AsyncJob" /> to execute.</param>
        /// <remarks>
        ///     For performance reasons, the internal execution method utilizes ConfigureAwait(false).
        /// </remarks>
        public static void QueueAsyncJob(AsyncJob asyncJob)
        {
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
        public static async Task WaitAsync() => await _WorkerSemaphore.WaitAsync(AbortToken);

        /// <summary>
        ///     Waits asynchronously until work is ready to be done, or until timeout is reached.
        /// </summary>
        /// <param name="timeout">
        ///     Maximum <see cref="TimeSpan" /> to wait until returning without successful wait.
        /// </param>
        /// <returns>
        ///     <c>true</c> if the wait did not exceed given timeout, otherwise <c>false</c>.
        /// </returns>
        public static async Task<bool> WaitAsync(TimeSpan timeout) => await _WorkerSemaphore.WaitAsync(timeout);

        /// <summary>
        ///     Aborts execution of job scheduler.
        /// </summary>
        /// <param name="abort">
        ///     Whether or not to abort <see cref="AsyncJobScheduler" /> execution.
        /// </param>
        public static void Abort(bool abort)
        {
            if (abort)
            {
                _AbortTokenSource.Cancel();
            }
        }

        #endregion


        #region Runtime

        private static async Task ExecuteJob(AsyncJob asyncJob)
        {
            Debug.Assert(asyncJob != null);

            // observe cancellation token
            if (_AbortTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                // if semaphore is empty, wait until it is released
                await _WorkerSemaphore.WaitAsync().ConfigureAwait(false);

                // signal JobStarted event
                OnJobStarted(asyncJob);

                // execute job without context dependence
                await asyncJob.Execute().ConfigureAwait(false);

                // signal JobFinished event
                OnJobFinished(asyncJob);
            }
            finally
            {
                // release semaphore regardless of any job errors
                _WorkerSemaphore.Release();
            }
        }

        private static async Task ExecuteInvocation(AsyncInvocation invocation)
        {
            Debug.Assert(invocation != null);

            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _WorkerSemaphore.WaitAsync().ConfigureAwait(false);

                await invocation.Invoke().ConfigureAwait(false);
            }
            finally
            {
                // release semaphore regardless of any invocation errors
                _WorkerSemaphore.Release();
            }
        }

        #endregion


        #region Events

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
