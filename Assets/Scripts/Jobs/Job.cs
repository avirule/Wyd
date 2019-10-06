#region

using System;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Jobs
{
    public class Job
    {
        protected readonly object Handle;
        protected readonly Action ExecutionAction;
        protected bool Done;

        public DateTime StartTime { get; private set; }
        public DateTime FinishTime { get; private set; }

        /// <summary>
        ///     Identity of <see cref="Job" />.
        /// </summary>
        public object Identity { get; private set; }

        /// <summary>
        ///     Token signalling cancellation of internal process.
        /// </summary>
        public CancellationToken AbortToken { get; private set; }

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

        internal virtual void Initialize(object identity, CancellationToken token)
        {
            Identity = identity;
            AbortToken = token;
        }

        /// <summary>
        ///     Begins executing the <see cref="Job" />
        /// </summary>
        public void Execute()
        {
            StartTime = DateTime.UtcNow;

            Process();
            ProcessFinished();

            FinishTime = DateTime.UtcNow;
            ExecutionTime = FinishTime - StartTime;

            IsDone = true;
            Finished?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void Process()
        {
            ExecutionAction?.Invoke();
        }

        protected virtual void ProcessFinished()
        {
        }
    }
}
