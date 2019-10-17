#region

using System;
using System.Threading;
using NLog;
using Wyd.System.Collections;
using Wyd.System.Logging;

#endregion

namespace Wyd.System.Jobs
{
    public class JobWorker
    {
        private readonly object _Handle;
        private readonly Thread _Thread;
        private readonly SpinLockCollection<Job> _ItemQueue;
        private readonly CancellationToken _AbortToken;

        public readonly TimeSpan WaitTimeout;
        private bool _Processing;

        public bool Running { get; private set; }

        public bool Processing
        {
            get
            {
                bool tmp;

                lock (_Handle)
                {
                    tmp = _Processing;
                }

                return tmp;
            }
            private set
            {
                lock (_Handle)
                {
                    _Processing = value;
                }
            }
        }

        public int ManagedThreadId => _Thread.ManagedThreadId;

        public event JobStartedEventHandler JobStarted;
        public event JobFinishedEventHandler JobFinished;

        public JobWorker(TimeSpan waitTimeout, CancellationToken abortToken)
        {
            _Handle = new object();
            _Thread = new Thread(ProcessItemQueue);

            _AbortToken = abortToken;
            _ItemQueue = new SpinLockCollection<Job>();
            WaitTimeout = waitTimeout;
        }

        public void Start()
        {
            _Thread.Start();
            Running = true;
        }

        public void QueueJob(Job job) => _ItemQueue.Add(job);

        private void ProcessItemQueue()
        {
            try
            {
                while (!_AbortToken.IsCancellationRequested)
                {
                    if (_ItemQueue.TryTake(out Job job, WaitTimeout, _AbortToken))
                    {
                        ProcessJob(job);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Thread aborted
                EventLogger.Log(LogLevel.Warn,
                    $"{nameof(JobWorker)} with id {ManagedThreadId} has critically aborted.");
            }
            finally
            {
                Running = false;
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
