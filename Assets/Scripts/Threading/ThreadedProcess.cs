#region

using System.Threading;

#endregion

namespace Threading
{
    public class ThreadedProcess
    {
        protected readonly object Handle;
        protected bool Done;
        public bool Die;

        protected ThreadedProcess()
        {
            Done = Die = false;
            Handle = new object();
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
            ThreadPool.QueueUserWorkItem(state => Run());
        }

        protected virtual void ThreadFunction()
        {
        }

        protected virtual void OnFinished()
        {
        }

        public virtual void Abort()
        {
            Die = true;
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