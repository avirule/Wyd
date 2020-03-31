#region

using System;
using System.Diagnostics;
using System.Threading;

#endregion

namespace Wyd.System.Jobs
{
    public class Job
    {
        private Stopwatch _Stopwatch;

        protected readonly object Handle;

        /// <summary>
        ///     Allows jobs to be created on-the-fly with delegates,
        ///     not necessitating an entire derived class for usage.
        /// </summary>
        protected readonly Action ExecutionAction;

        protected bool Done;

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
        public Job() => (Handle, Done) = (new object(), false);

        public Job(Action action) : this() => ExecutionAction = action;

        /// <summary>
        ///     Thread-safe determination of execution status.
        /// </summary>
        public bool IsDone
        {
            get
            {
                bool tmp;

                lock (Handle)
                {
                    tmp = Done;
                }

                return tmp;
            }
            protected set
            {
                lock (Handle)
                {
                    Done = value;
                }
            }
        }

        internal virtual void Initialize(object identity, CancellationToken abortToken)
        {
            _Stopwatch = new Stopwatch();
            Identity = identity;
            AbortToken = abortToken;
        }

        /// <summary>
        ///     Begins executing the <see cref="Job" />.
        /// </summary>
        public void Execute()
        {
            _Stopwatch.Restart();

            Process();

            ProcessTime = _Stopwatch.Elapsed;

            ProcessFinished();

            ExecutionTime = _Stopwatch.Elapsed;
            _Stopwatch.Stop();
            _Stopwatch.Reset();

            IsDone = true;
            Finished?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     This is the main method that is executed in
        ///     the <see cref="JobWorker" />'s threaded context.
        /// </summary>
        protected virtual void Process()
        {
            ExecutionAction?.Invoke();
        }

        /// <summary>
        ///     The final method, run after <see cref="Process" />
        ///     in the <see cref="JobWorker" />'s threaded context.
        /// </summary>
        protected virtual void ProcessFinished() { }
    }
}
