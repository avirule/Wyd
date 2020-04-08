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
        private Stopwatch _Stopwatch;

        /// <summary>
        ///     Allows jobs to be created on-the-fly with delegates,
        ///     not necessitating an entire derived class for usage.
        /// </summary>
        protected readonly Func<Task> Invocation;

        /// <summary>
        ///     Identity of <see cref="Job" />.
        /// </summary>
        public object Identity { get; private set; }

        /// <summary>
        ///     Token signalling cancellation of internal process.
        /// </summary>
        public CancellationToken AbortToken { get; private set; }

        /// <summary>
        ///     Elapsed time of specifically the <see cref="Process" /> function.
        /// </summary>
        public TimeSpan ProcessTime { get; private set; }

        /// <summary>
        ///     Total elapsed time of execution in milliseconds.
        /// </summary>
        public TimeSpan ExecutionTime { get; private set; }

        public event EventHandler Finished;

        /// <summary>
        ///     Instantiates a new instance of the <see cref="Job" /> class.
        /// </summary>
        public AsyncJob() => Done = false;

        public AsyncJob(Func<Task> invocation) : this() => Invocation = invocation;

        /// <summary>
        ///     Thread-safe determination of execution status.
        /// </summary>
        public bool Done { get; set; }

        internal virtual void Initialize(object identity, CancellationToken abortToken)
        {
            _Stopwatch = new Stopwatch();

            Identity = identity;
            AbortToken = abortToken;
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

            Done = true;
            Finished?.Invoke(this, EventArgs.Empty);
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
