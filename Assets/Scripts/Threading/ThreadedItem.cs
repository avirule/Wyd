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

        public long ExecutionTime => _ExecutionTimer.ElapsedMilliseconds;

        public object Identity { get; internal set; }

        public ThreadedItem()
        {
            _Handle = new object();
            _ExecutionTimer = new Stopwatch();
            Done = false;
        }

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