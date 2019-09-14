#region

using System;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Threading.ThreadedItems
{
    public abstract class ThreadedItem
    {
        private readonly object _Handle;
        protected bool Done;

        public DateTime StartTime { get; private set; }
        public DateTime FinishTime { get; private set; }

        /// <summary>
        ///     Identity of <see cref="ThreadedItem" />.
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
        ///     Instantiates a new instance of the <see cref="ThreadedItem" /> class.
        /// </summary>
        public ThreadedItem()
        {
            _Handle = new object();
            Done = false;
        }

        /// <summary>
        ///     Thread-safe determination of execution status.
        /// </summary>
        public bool IsDone
        {
            get
            {
                bool tmp;

                lock (_Handle)
                {
                    tmp = Done;
                }

                return tmp;
            }
            protected set
            {
                lock (_Handle)
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
        ///     Begins executing the <see cref="ThreadedItem" />
        /// </summary>
        public virtual Task Execute()
        {
            StartTime = DateTime.UtcNow;

            Process();
            ProcessFinished();

            FinishTime = DateTime.UtcNow;
            ExecutionTime = FinishTime - StartTime;

            IsDone = true;
            Finished?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }

        protected virtual void Process()
        {
        }

        protected virtual void ProcessFinished()
        {
        }
    }
}
