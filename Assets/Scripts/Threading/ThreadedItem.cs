#region

using System;
using System.Threading.Tasks;

#endregion

namespace Threading
{
    public abstract class ThreadedItem
    {
        private readonly object _Handle;
        protected bool Done;

        public long MaximumLifetime { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime FinishTime { get; private set; }
        public DateTime ExpirationTime { get; private set; }

        /// <summary>
        ///     Total elapsed time of execution in milliseconds.
        /// </summary>
        public TimeSpan ExecutionTime { get; private set; }

        /// <summary>
        ///     Identity of <see cref="ThreadedItem" />.
        /// </summary>
        public object Identity { get; internal set; }

        public event EventHandler Finished;

        /// <summary>
        ///     Instantiates a new instance of the <see cref="ThreadedItem" /> class.
        /// </summary>
        public ThreadedItem(long maximumLifetime = 30000)
        {
            _Handle = new object();
            Done = false;
            MaximumLifetime = maximumLifetime;
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

        /// <summary>
        ///     Begins executing the <see cref="ThreadedItem" />
        /// </summary>
        public virtual Task Execute()
        {
            StartTime = DateTime.Now;

            Process();

            FinishTime = DateTime.Now;
            ExpirationTime = FinishTime.AddMilliseconds(MaximumLifetime);
            ExecutionTime = FinishTime - StartTime;

            IsDone = true;
            Finished?.Invoke(this, EventArgs.Empty);

            return Task.CompletedTask;
        }

        protected virtual void Process()
        {
        }
    }
}