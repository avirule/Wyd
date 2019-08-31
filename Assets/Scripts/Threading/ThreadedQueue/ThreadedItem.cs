#region

using System;
using System.Diagnostics;
using System.Threading.Tasks;

#endregion

namespace Threading.ThreadedQueue
{
    public abstract class ThreadedItem
    {
        private readonly object _Handle;
        private readonly Stopwatch _ExecutionTimer;
        protected bool Done;

        /// <summary>
        ///     Total elapsed time of execution in milliseconds.
        /// </summary>
        public TimeSpan ExecutionTime => _ExecutionTimer.Elapsed;

        /// <summary>
        ///     Time at which execution finished.
        /// </summary>
        public DateTime ExecutionFinishTime { get; private set; }

        /// <summary>
        ///     Identity of <see cref="ThreadedItem" />.
        /// </summary>
        public object Identity { get; internal set; }

        /// <summary>
        ///     Instantiates a new instance of the <see cref="ThreadedItem" /> class.
        /// </summary>
        public ThreadedItem()
        {
            _Handle = new object();
            _ExecutionTimer = new Stopwatch();
            Done = false;
            ExecutionFinishTime = DateTime.MaxValue;
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
            _ExecutionTimer.Reset();
            _ExecutionTimer.Start();

            Process();

            _ExecutionTimer.Stop();
            ExecutionFinishTime = DateTime.Now;

            IsDone = true;

            return Task.CompletedTask;
        }

        protected virtual void Process()
        {
        }
    }
}