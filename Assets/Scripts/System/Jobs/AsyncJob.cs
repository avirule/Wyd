#region

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Wyd.System.Jobs
{
    public class AsyncJob
    {
        private readonly Stopwatch _Stopwatch;

        /// <summary>
        ///     Allows jobs to be created on-the-fly with delegates,
        ///     not necessitating an entire derived class for usage.
        /// </summary>
        protected readonly Func<Task> Invocation;

        /// <summary>
        ///     Identity of <see cref="Job" />.
        /// </summary>
        public object Identity { get; }

        /// <summary>
        ///     Token that can be passed into constructor to allow jobs to observe cancellation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

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

        /// <summary>
        ///     Instantiates a new instance of the <see cref="Job" /> class.
        /// </summary>
        public AsyncJob(CancellationToken cancellationToken, Func<Task> invocation = null)
        {
            _Stopwatch = new Stopwatch();
            Identity = Guid.NewGuid();
            CancellationToken = cancellationToken;
            Invocation = invocation ?? (() => Task.CompletedTask);
            IsWorkFinished = false;
        }

        /// <summary>
        ///     Begins executing the <see cref="Job" />.
        /// </summary>
        public async Task Execute()
        {
            _Stopwatch.Restart();

            await Process();

            ProcessTime = _Stopwatch.Elapsed;

            await ProcessFinished();

            ExecutionTime = _Stopwatch.Elapsed;
            _Stopwatch.Stop();
            _Stopwatch.Reset();

            IsWorkFinished = true;
            WorkFinished?.Invoke(this, new AsyncJobEventArgs(this));
        }

        /// <summary>
        ///     This is the main method that is executed.
        /// </summary>
        protected virtual async Task Process()
        {
            if (Invocation != null)
            {
                await Invocation();
            }
        }

        /// <summary>
        ///     The final method, run after <see cref="Process" />.
        /// </summary>
        protected virtual Task ProcessFinished() => Task.CompletedTask;
    }
}
