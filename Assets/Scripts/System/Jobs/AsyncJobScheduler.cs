#region

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;

#endregion

namespace Wyd.System.Jobs
{
    public static class AsyncJobScheduler
    {
        private static readonly CancellationTokenSource _AbortTokenSource;
        private static readonly ChannelReader<AsyncJob> _Reader;
        private static readonly ChannelWriter<AsyncJob> _Writer;
        private static readonly ConcurrentStack<CancellationTokenSource> _WorkerCancellationTokens;

        private static long _asyncWorkerCount;
        private static long _jobsQueued;
        private static long _processingJobCount;

        public static CancellationToken AbortToken => _AbortTokenSource.Token;
        public static long AsyncWorkerCount => Interlocked.Read(ref _asyncWorkerCount);
        public static long JobsQueued => Interlocked.Read(ref _jobsQueued);
        public static long ProcessingJobCount => Interlocked.Read(ref _processingJobCount);

        /// <summary>
        ///     Initializes a new instance of <see cref="AsyncJobScheduler" /> class.
        /// </summary>
        static AsyncJobScheduler()
        {
            _AbortTokenSource = new CancellationTokenSource();
            Channel<AsyncJob> channel = Channel.CreateUnbounded<AsyncJob>();
            _Reader = channel.Reader;
            _Writer = channel.Writer;
            _WorkerCancellationTokens = new ConcurrentStack<CancellationTokenSource>();

            JobQueued += (sender, args) => Interlocked.Increment(ref _jobsQueued);
            JobStarted += (sender, args) => Interlocked.Increment(ref _processingJobCount);
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _jobsQueued);
                Interlocked.Decrement(ref _processingJobCount);
            };
        }


        #region State

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
        ///     Modifies total number of available workers for the internal job queue.
        /// </summary>
        /// <remarks>
        ///     This separate-method approach is taken to make intent clear, and to
        ///     more idiomatically constrain the total to a positive value.
        ///     Additionally, modifying this value causes an abrupt cancel on all active workers.
        ///     Therefore, it's advised to not use it unless absolutely necessary.
        /// </remarks>
        /// <param name="newWorkerCount">Modified count of workers for <see cref="AsyncJobScheduler" /> to initialize.</param>
        public static void ModifyActiveAsyncWorkerCount(ulong newWorkerCount)
        {
            if (newWorkerCount == 0)
            {
                return;
            }

            Interlocked.Exchange(ref _asyncWorkerCount, (long)newWorkerCount);

            while (_WorkerCancellationTokens.TryPop(out CancellationTokenSource tokenSource))
            {
                tokenSource.Cancel();
            }

            for (int i = 0; i < AsyncWorkerCount; i++)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                _WorkerCancellationTokens.Push(tokenSource);

                Task.Run(async () => await ProcessItemQueue(tokenSource.Token), AbortToken);
            }
        }

        public static async Task QueueAsyncJob(AsyncJob asyncJob)
        {
            if (AbortToken.IsCancellationRequested)
            {
                return;
            }

            if (AsyncWorkerCount == 0)
            {
                Log.Warning(
                    $"{nameof(AsyncWorkerCount)} is 0. Any jobs queued will not be completed until `{nameof(ModifyActiveAsyncWorkerCount)}()` is called with a non-zero value.");
            }

            await _Writer.WriteAsync(asyncJob, AbortToken);
            OnJobQueued(new AsyncJobEventArgs(asyncJob));
        }

        #endregion


        #region Runtime

        private static async Task ProcessItemQueue(CancellationToken token)
        {
            try
            {
                CancellationToken combinedToken =
                    CancellationTokenSource.CreateLinkedTokenSource(AbortToken, token).Token;

                while (await _Reader.WaitToReadAsync(combinedToken))
                {
                    while (!combinedToken.IsCancellationRequested && _Reader.TryRead(out AsyncJob asyncJob))
                    {
                        if (asyncJob == null)
                        {
                            continue;
                        }

                        await ExecuteJob(asyncJob);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error($"Error in {nameof(AsyncJobScheduler)}: {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private static async Task ExecuteJob(AsyncJob asyncJob)
        {
            OnJobStarted(new AsyncJobEventArgs(asyncJob));
            await asyncJob.Execute();
            OnJobFinished(new AsyncJobEventArgs(asyncJob));
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
