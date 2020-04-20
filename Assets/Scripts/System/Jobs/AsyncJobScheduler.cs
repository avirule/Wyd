#region

using System;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Jobs
{
    public static class AsyncJobScheduler
    {
        private static CancellationTokenSource _AbortTokenSource;
        private static Semaphore _WorkerSemaphore;
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

            JobQueued += (sender, args) => Interlocked.Increment(ref _QueuedJobs);
            JobStarted += (sender, args) => Interlocked.Increment(ref _ProcessingJobs);
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _QueuedJobs);
                Interlocked.Decrement(ref _ProcessingJobs);
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

            OnLogged(1,
                $"Modifying {nameof(MaximumProcessingJobs)}: from '{_MaximumProcessingJobs}' to '{newWorkerCount}'.");

            Interlocked.Exchange(ref _MaximumProcessingJobs, newWorkerCount);

            _AbortTokenSource.Cancel();
            _AbortTokenSource = new CancellationTokenSource();
            _WorkerSemaphore = new Semaphore((int)MaximumProcessingJobs, (int)MaximumProcessingJobs);
        }

        public static async Task QueueAsyncJob(AsyncJob asyncJob)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            if (MaximumProcessingJobs == 0)
            {
                OnLogged(3,
                    $"{nameof(MaximumProcessingJobs)} is 0. Any jobs queued will not be completed until `{nameof(ModifyMaximumProcessingJobCount)}()` is called with a non-zero value.");
            }

            OnLogged(1, $"Queued new {nameof(AsyncJob)} for completion (type: {asyncJob.GetType().Name})");

            OnJobQueued(new AsyncJobEventArgs(asyncJob));

            await ExecuteJob(asyncJob);
        }

        #endregion


        #region Runtime

        private static async Task ExecuteJob(AsyncJob asyncJob)
        {
            if (_AbortTokenSource.IsCancellationRequested)
            {
                return;
            }

            _WorkerSemaphore.WaitOne();

            OnJobStarted(new AsyncJobEventArgs(asyncJob));

            await asyncJob.Execute();

            OnJobFinished(new AsyncJobEventArgs(asyncJob));

            _WorkerSemaphore.Release();
        }

        #endregion


        #region Events

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

        public static event AsyncJobSchedulerLogEventHandler Logged;


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

        private static void OnLogged(byte logLevel, string logText)
        {
            Logged?.Invoke(Logged, new AsyncJobSchedulerLogEventArgs(logLevel, logText));
        }

        #endregion
    }
}
