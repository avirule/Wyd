#region

using System.Diagnostics;

#endregion

namespace Threading
{
    public abstract class ThreadedItem
    {
        private readonly object _Handle;
        private readonly Stopwatch _ExecutionTimer;
        protected bool Done;

        /// <summary>
        ///     Total elapsed time of execution in milliseconds.
        /// </summary>
        public long ExecutionTime => _ExecutionTimer.ElapsedMilliseconds;

        /// <summary>
        ///     Identity of <see cref="Threading.ThreadedItem" />.
        /// </summary>
        public object Identity { get; internal set; }

        /// <summary>
        ///     Instantiates a new instance of the <see cref="Threading.ThreadedItem" /> class.
        /// </summary>
        public ThreadedItem()
        {
            _Handle = new object();
            _ExecutionTimer = new Stopwatch();
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

        /// <summary>
        ///     Begins executing the <see cref="Threading.ThreadedItem" />
        /// </summary>
        public virtual void Execute()
        {
            _ExecutionTimer.Reset();
            _ExecutionTimer.Start();

            Process();

            _ExecutionTimer.Stop();

            IsDone = true;
        }

        protected virtual void Process()
        {
        }
    }
}