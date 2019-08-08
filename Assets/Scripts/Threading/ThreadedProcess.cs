using System.Threading;

namespace Threading
{
    public class ThreadedProcess
    {
        protected readonly object Handle;
        protected bool Done;
        protected Thread Thread;

        public ThreadedProcess()
        {
            Done = false;
            Handle = new object();
            Thread = null;
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
            Thread = new Thread(Run);
            Thread.Start();
        }

        public virtual void Abort()
        {
            Thread.Abort();
        }

        protected virtual void ThreadFunction()
        {
        }

        protected virtual void OnFinished()
        {
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

        public void Run()
        {
            ThreadFunction();

            IsDone = true;
        }
    }
}