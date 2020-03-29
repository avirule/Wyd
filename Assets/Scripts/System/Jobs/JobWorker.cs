#region

using System;
using System.Threading;
using Serilog;
using Wyd.System.Collections;

#endregion

namespace Wyd.System.Jobs
{
    public class JobWorker
    {
        private readonly object _Handle;
        private readonly SpinLockCollection<Job> _ItemQueue;
        private readonly CancellationToken _AbortToken;

        public readonly TimeSpan WaitTimeout;
        private bool _Processing;

        public Thread InternalThread { get; }

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

        public event JobStartedEventHandler JobStarted;
        public event JobEventHandler JobFinished;

        public JobWorker(TimeSpan waitTimeout, CancellationToken abortToken)
        {
            _Handle = new object();
            InternalThread = new Thread(ProcessItemQueue);

            _AbortToken = abortToken;
            _ItemQueue = new SpinLockCollection<Job>();
            WaitTimeout = waitTimeout;
        }

        public void Start()
        {
            InternalThread.Start();
            Running = true;
        }

        /// <summary>
        ///     Used to forcefully abort the thread. Use ONLY IF NECESSARY.
        /// </summary>
        /// <param name="quit">Whether or not to forcefully abort worker.</param>
        public void ForceAbort(bool quit)
        {
            if (!quit)
            {
                return;
            }

            InternalThread.Abort();
            Log.Warning($"{nameof(JobWorker)} ID {InternalThread.ManagedThreadId} forced to abort.");
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
                Log.Warning($"{nameof(JobWorker)} (ID {InternalThread.ManagedThreadId}) has critically aborted.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {nameof(JobWorker)} (ID {InternalThread.ManagedThreadId}): {ex.Message}\r\n{ex.StackTrace}");
            }
            finally
            {
                Running = Processing = false;
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
