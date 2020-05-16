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
    /// <summary>
    ///     This class can be used to instantiate work on the .NET ThreadPool, utilizing the <see cref="AsyncJob"/>
    ///      class—or the <see cref="AsyncInvocation"/>—as a convenient way to deliver completed work and relevant work data to
    ///      any interested subscriptors, all without starving the system at large of CPU time.
    /// </summary>
    /// <remarks>
    ///    The class also utilizes a semaphore to ensure the work being done leaves multiple
    ///     cores unstressed, so any other critical processes aren't interfered with.
    ///
    ///    Generally, the maximum number of concurrent jobs that can run is limited to {logical core count - 2},
    ///     and in my testing, being able to increase the amount of concurrent work only leads to resource shortages
    ///     for other processes (in the case of a game, it will lead to significant frame rate drop as the main core is
    ///     consumed for other tasks).
    ///
    ///    However, in the interest of usability, I may add functionality to meaningfully increase the
    ///     cap later on.
    /// </remarks>
    public static class AsyncJobScheduler
    {
        /// <summary>
        ///     Global cancellation token source to provide an observable <see cref="CancellationToken"/> that cancels
        ///      when the <see cref="AsyncJobScheduler"/> is aborted.
        /// </summary>
        private static readonly CancellationTokenSource _AbortTokenSource;

        /// <summary>
        ///     Limits the total number of jobs or invocations that can execute concurrently.
        /// </summary>
        private static readonly SemaphoreSlim _ConcurrentWorkSemaphore;

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
            _ConcurrentWorkSemaphore = new SemaphoreSlim(MaximumConcurrentJobs, MaximumConcurrentJobs);

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
                asyncJob.Cancel();
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
        public static async Task WaitAsync() => await _ConcurrentWorkSemaphore.WaitAsync(AbortToken);

        /// <summary>
        ///     Waits asynchronously until work is ready to be done, or until timeout is reached.
        /// </summary>
        /// <param name="timeout">
        ///     Maximum <see cref="TimeSpan" /> to wait until returning without successful wait.
        /// </param>
        /// <returns>
        ///     <c>true</c> if the wait did not exceed given timeout, otherwise <c>false</c>.
        /// </returns>
        public static async Task<bool> WaitAsync(TimeSpan timeout) => await _ConcurrentWorkSemaphore.WaitAsync(timeout);

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
                asyncJob.Cancel();
                return;
            }

            try
            {
                // if semaphore is empty, wait until it is released
                await _ConcurrentWorkSemaphore.WaitAsync().ConfigureAwait(false);

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
                _ConcurrentWorkSemaphore.Release();
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
                await _ConcurrentWorkSemaphore.WaitAsync().ConfigureAwait(false);

                await invocation.Invoke().ConfigureAwait(false);
            }
            finally
            {
                // release semaphore regardless of any invocation errors
                _ConcurrentWorkSemaphore.Release();
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
