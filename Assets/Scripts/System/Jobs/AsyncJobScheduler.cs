#region

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Jobs
{
    public static class AsyncJobScheduler
    {
        private static readonly CancellationTokenSource _abortTokenSource;
        private static readonly ChannelReader<AsyncJob> _reader;
        private static readonly ChannelWriter<AsyncJob> _writer;
        private static readonly ConcurrentStack<CancellationTokenSource> _workerCancellationTokens;

        private static long _AsyncWorkerCount;
        private static long _JobsQueued;
        private static long _ProcessingJobCount;

        public static CancellationToken AbortToken => _abortTokenSource.Token;
        public static long AsyncWorkerCount => Interlocked.Read(ref _AsyncWorkerCount);
        public static long JobsQueued => Interlocked.Read(ref _JobsQueued);
        public static long ProcessingJobCount => Interlocked.Read(ref _ProcessingJobCount);

        /// <summary>
        ///     Initializes a new instance of <see cref="AsyncJobScheduler" /> class.
        /// </summary>
        static AsyncJobScheduler()
        {
            _abortTokenSource = new CancellationTokenSource();
            Channel<AsyncJob> channel = Channel.CreateUnbounded<AsyncJob>();
            _reader = channel.Reader;
            _writer = channel.Writer;
            _workerCancellationTokens = new ConcurrentStack<CancellationTokenSource>();

            JobQueued += (sender, args) => Interlocked.Increment(ref _JobsQueued);
            JobStarted += (sender, args) => Interlocked.Increment(ref _ProcessingJobCount);
            JobFinished += (sender, args) =>
            {
                Interlocked.Decrement(ref _JobsQueued);
                Interlocked.Decrement(ref _ProcessingJobCount);
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
                _abortTokenSource.Cancel();
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

            OnLogged(1, $"Modifying worker count: from '{_AsyncWorkerCount}' to '{newWorkerCount}'.");

            Interlocked.Exchange(ref _AsyncWorkerCount, (long)newWorkerCount);

            while (_workerCancellationTokens.TryPop(out CancellationTokenSource tokenSource))
            {
                tokenSource.Cancel();
            }

            for (int i = 0; i < AsyncWorkerCount; i++)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                _workerCancellationTokens.Push(tokenSource);

                Task.Factory.StartNew(async () => await ProcessItemQueue(tokenSource.Token),
                    TaskCreationOptions.LongRunning);

                //Task.Run(async () => await ProcessItemQueue(tokenSource.Token), AbortToken);
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
                OnLogged(3,
                    $"{nameof(AsyncWorkerCount)} is 0. Any jobs queued will not be completed until `{nameof(ModifyActiveAsyncWorkerCount)}()` is called with a non-zero value.");
            }

            OnLogged(1, $"Queued new {nameof(AsyncJob)} for completion (type: {asyncJob.GetType().Name})");

            await _writer.WriteAsync(asyncJob, AbortToken);
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

                while (await _reader.WaitToReadAsync(combinedToken))
                {
                    while (!combinedToken.IsCancellationRequested && _reader.TryRead(out AsyncJob asyncJob))
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
                OnLogged(4, $"Error in {nameof(AsyncJobScheduler)}: {ex.Message}\r\n{ex.StackTrace}");
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
