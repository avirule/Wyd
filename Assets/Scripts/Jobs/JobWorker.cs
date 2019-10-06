#region

using System;
using System.Collections.Concurrent;
using System.Threading;

#endregion

namespace Jobs
{
    public class JobWorker
    {
        private readonly Thread _Thread;
        private readonly BlockingCollection<Job> _ItemQueue;
        private readonly CancellationToken _AbortToken;

        public readonly int WaitTimeout;

        public bool Running { get; private set; }
        public bool Processing { get; private set; }
        public int ManagedThreadId => _Thread.ManagedThreadId;

        public event JobStartedEventHandler JobStarted;
        public event JobFinishedEventHandler JobFinished;

        public JobWorker(int waitTimeout, CancellationToken abortToken)
        {
            _Thread = new Thread(ProcessItemQueue);
            _ItemQueue = new BlockingCollection<Job>();
            _AbortToken = abortToken;

            WaitTimeout = waitTimeout;
        }

        public void Start()
        {
            _Thread.Start();
            Running = true;
        }

        public bool QueueJob(Job job) => _ItemQueue.TryAdd(job);

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
                    Running = false;
                    return;
                }
            }
        }

        private void ProcessJob(Job job)
        {
            Processing = true;

            OnJobStarted(this, new JobEventArgs(job));
            job.Execute();
            OnJobFinished(this, new JobEventArgs(job));

            Processing = false;
        }

        private void OnJobStarted(object sender, JobEventArgs args)
        {
            JobStarted?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, JobEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
        }
    }
}
