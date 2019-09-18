#region

using System;
using System.Collections.Concurrent;
using System.Threading;

#endregion

namespace Jobs
{
    public class JobCompletionThread
    {
        private readonly Thread _InternalThread;
        private readonly BlockingCollection<Job> _ItemQueue;
        private readonly CancellationToken _AbortToken;

        public int WaitTimeout;

        public bool Running { get; private set; }
        public bool Processing { get; private set; }
        public int ManagedThreadId => _InternalThread.ManagedThreadId;

        public event EventHandler<JobFinishedEventArgs> JobFinished;

        public JobCompletionThread(int waitTimeout, CancellationToken abortToken)
        {
            _InternalThread = new Thread(ProcessItemQueue);
            _ItemQueue = new BlockingCollection<Job>();
            _AbortToken = abortToken;

            WaitTimeout = waitTimeout;
        }

        public void Start()
        {
            _InternalThread.Start();
            Running = true;
        }

        public bool QueueJob(Job job)
        {
            return _ItemQueue.TryAdd(job);
        }

        private void ProcessItemQueue()
        {
            while (!_AbortToken.IsCancellationRequested)
            {
                try
                {
                    if (_ItemQueue.TryTake(out Job job, WaitTimeout, _AbortToken))
                    {
                        ProcessJob(job);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Thread aborted
                    return;
                }
            }
        }

        private void ProcessJob(Job job)
        {
            Processing = true;

            job.Execute();
            OnJobFinished(this, new JobFinishedEventArgs(job));

            Processing = false;
        }

        private void OnJobFinished(object sender, JobFinishedEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
        }
    }
}
