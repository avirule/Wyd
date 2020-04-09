#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Wyd.System.Collections;

#endregion

namespace Wyd.System.Jobs
{
    public static class AsyncJobScheduler
    {
        private static readonly CancellationTokenSource _AbortTokenSource;
        private static readonly AsyncCollection<AsyncJob> _AsyncJobQueue;

        private static int _workerThreadCount;


        private static long _jobsQueued;
        private static long _processingJobCount;

        public static CancellationToken AbortToken => _AbortTokenSource.Token;
        public static int WorkerThreadCount => _workerThreadCount;
        public static long JobsQueued => Interlocked.Read(ref _jobsQueued);
        public static long ProcessingJobCount => Interlocked.Read(ref _processingJobCount);

        /// <summary>
        ///     Initializes a new instance of <see cref="AsyncJobScheduler" /> class.
        /// </summary>
        static AsyncJobScheduler()
        {
            ModifyWorkerThreadCount(Environment.ProcessorCount - 1 /* to facilitate main thread */);

            _AsyncJobQueue = new AsyncCollection<AsyncJob>();
            _AbortTokenSource = new CancellationTokenSource();

            JobQueued += (sender, args) => Interlocked.Increment(ref _jobsQueued);
            JobStarted += (sender, args) => Interlocked.Increment(ref _processingJobCount);
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _jobsQueued);
                Interlocked.Decrement(ref _processingJobCount);
            };

            for (int i = 0; i < WorkerThreadCount; i++)
            {
                Task.Run(ProcessItemQueue, AbortToken);
            }
        }


        #region STATE

        /// <summary>
        ///     Aborts execution of internal threaded process.
        /// </summary>
        public static void Abort(bool abort)
        {
            if (abort)
            {
                _AbortTokenSource.Cancel();
            }
        }

        /// <summary>
        ///     Modifies total number of available worker threads for JobQueue.
        /// </summary>
        /// <remarks>
        ///     This separate-method approach is taken to make intent clear, and to
        ///     more idiomatically constrain the total to a positive value.
        /// </remarks>
        /// <param name="newTotal"></param>
        private static void ModifyWorkerThreadCount(int newTotal)
        {
            Interlocked.Exchange(ref _workerThreadCount, Math.Max(newTotal, 1));
        }

        public static async Task QueueAsyncJob(AsyncJob asyncJob)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            await _AsyncJobQueue.PushAsync(asyncJob, AbortToken);
            OnJobQueued(new AsyncJobEventArgs(asyncJob));
        }

        #endregion


        #region RUNTIME

        private static async Task ProcessItemQueue()
        {
            try
            {
                while (!AbortToken.IsCancellationRequested)
                {
                    AsyncJob asyncJob = await _AsyncJobQueue.TakeAsync(AbortToken);

                    if (asyncJob == null)
                    {
                        continue;
                    }

                    await ExecuteJob(asyncJob);
                }
            }
            catch (OperationCanceledException)
            {
                // Thread aborted
                Log.Warning($"{nameof(AsyncJobScheduler)} has critically aborted.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {nameof(AsyncJobScheduler)}: {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private static async Task ExecuteJob(AsyncJob asyncJob)
        {
            OnJobStarted(new AsyncJobEventArgs(asyncJob));
            await asyncJob.Execute();
            OnJobFinished(new AsyncJobEventArgs(asyncJob));
        }

        #endregion


        #region EVENTS

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
