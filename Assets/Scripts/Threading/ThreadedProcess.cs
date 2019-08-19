#region

using System.Threading;

#endregion

namespace Threading
{
    public class ThreadedProcess
    {
        protected readonly Thread Thread;
        protected readonly object Handle;
        protected bool Done;

        protected ThreadedProcess()
        {
            Thread = new Thread(Run);
            Handle = new object();
            Done = false;
        }

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
            set
            {
                lock (Handle)
                {
                    Done = value;
                }
            }
        }

        public virtual void Start()
        {
            Thread.Start();
        }

        protected virtual void ThreadFunction()
        {
        }

        protected virtual void OnFinished()
        {
        }

        public virtual void Abort()
        {
            Thread.Abort();

            IsDone = true;
        }

        public virtual bool Update()
        {
            if (!IsDone)
            {
                return false;
            }

            OnFinished();

            return true;
        }

        protected void Run()
        {
            ThreadFunction();

            IsDone = true;
        }
    }
}