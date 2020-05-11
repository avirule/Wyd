#region

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

#endregion

namespace Wyd.Jobs
{
    public class AsyncJob
    {
        private readonly Stopwatch _Stopwatch;

        /// <summary>
        ///     Identity of the <see cref="AsyncJob" />.
        /// </summary>
        public object Identity { get; }

        /// <summary>
        ///     Token that can be passed into constructor to allow jobs to observe cancellation.
        /// </summary>
        public CancellationToken CancellationToken { get; protected set; }

        /// <summary>
        ///     Thread-safe determination of execution status.
        /// </summary>
        public bool IsWorkFinished { get; set; }

        /// <summary>
        ///     Elapsed time of specifically the <see cref="Process" /> function.
        /// </summary>
        public TimeSpan ProcessTime { get; private set; }

        /// <summary>
        ///     Total elapsed time of execution in milliseconds.
        /// </summary>
        public TimeSpan ExecutionTime { get; private set; }

        public event AsyncJobEventHandler WorkFinished;

        internal Task ProcessInstanced { get; }

        public AsyncJob()
        {
            _Stopwatch = new Stopwatch();
            Identity = Guid.NewGuid();
            CancellationToken = AsyncJobScheduler.AbortToken;
            IsWorkFinished = false;

            ProcessInstanced = Execute();
        }

        /// <summary>
        ///     Instantiates a new instance of the <see cref="AsyncJob" /> class.
        /// </summary>
        public AsyncJob(CancellationToken cancellationToken)
        {
            _Stopwatch = new Stopwatch();
            Identity = Guid.NewGuid();
            CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken, cancellationToken).Token;
            IsWorkFinished = false;

            ProcessInstanced = Execute();
        }

        /// <summary>
        ///     Begins executing the <see cref="AsyncJob" />.
        /// </summary>
        private async Task Execute()
        {
            Debug.Assert(ProcessInstanced != null);

            try
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _Stopwatch.Restart();

                await Process().ConfigureAwait(false);

                ProcessTime = _Stopwatch.Elapsed;

                await ProcessFinished().ConfigureAwait(false);

                _Stopwatch.Stop();

                ExecutionTime = _Stopwatch.Elapsed;

                IsWorkFinished = true;
                WorkFinished?.Invoke(this, new AsyncJobEventArgs(this));
            }
            catch (Exception ex)
            {
                Log.Error($"Error in {nameof(AsyncJob)} execution: {ex.Message}\r\n{ex.StackTrace}");
                IsWorkFinished = true;
            }
        }

        /// <summary>
        ///     This is the main method that is executed.
        /// </summary>
        protected virtual Task Process() => Task.CompletedTask;

        /// <summary>
        ///     The final method, run after <see cref="Process" />.
        /// </summary>
        protected virtual Task ProcessFinished() => Task.CompletedTask;
    }
}
