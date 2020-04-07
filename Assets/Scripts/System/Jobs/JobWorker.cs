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
        private readonly Thread _InternalThread;
        private readonly SpinLockCollection<Job> _JobQueue;
        private readonly CancellationToken _AbortToken;
        private readonly AutoResetEvent _WorkFinishedResetEvent;

        private bool _Executing;
        private long _JobsQueued;

        public TimeSpan WaitTimeout { get; }

        public int ManagedThreadID => _InternalThread.ManagedThreadId;
        public ThreadState State => _InternalThread.ThreadState;
        public bool Running => _InternalThread.IsAlive;
        public bool Waiting => !Executing && (JobsQueued == 0);

        public bool Executing
        {
            get
            {
                bool tmp;

                lock (_Handle)
                {
                    tmp = _Executing;
                }

                return tmp;
            }
            private set
            {
                lock (_Handle)
                {
                    _Executing = value;
                }
            }
        }

        public long JobsQueued => Interlocked.Read(ref _JobsQueued);

        public event JobEventHandler JobStarted;
        public event JobEventHandler JobFinished;

        public JobWorker(TimeSpan waitTimeout, CancellationToken abortToken, AutoResetEvent workFinishedResetEvent)
        {
            _Handle = new object();
            _InternalThread = new Thread(ProcessItemQueue)
            {
                Priority = ThreadPriority.BelowNormal
            };

            _AbortToken = abortToken;
            _JobQueue = new SpinLockCollection<Job>();
            _WorkFinishedResetEvent = workFinishedResetEvent;
            WaitTimeout = waitTimeout;
        }

        public void Start()
        {
            _InternalThread.Start();
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

            _InternalThread.Abort();
            Log.Warning($"{nameof(JobWorker)} ID {ManagedThreadID} forced to abort.");
        }

        public void QueueJob(Job job)
        {
            Interlocked.Increment(ref _JobsQueued);
            _JobQueue.Add(job);
        }

        private void ProcessItemQueue()
        {
            try
            {
                while (!_AbortToken.IsCancellationRequested)
                {
                    if (!_JobQueue.TryTake(out Job job, WaitTimeout, _AbortToken))
                    {
                        continue;
                    }

                    ExecuteJob(job);
                    Interlocked.Decrement(ref _JobsQueued);

                    if (_JobsQueued == 0)
                    {
                        _WorkFinishedResetEvent.Set();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Thread aborted
                Log.Warning($"{nameof(JobWorker)} (ID {ManagedThreadID}) has critically aborted.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {nameof(JobWorker)} (ID {ManagedThreadID}): {ex.Message}\r\n{ex.StackTrace}");
            }
            finally
            {
                Executing = false;
            }
        }

        private void ExecuteJob(Job job)
        {
            Executing = true;

            OnJobStarted(this, new JobEventArgs(job));
            job.Execute();
            OnJobFinished(this, new JobEventArgs(job));

            Executing = false;
        }

        public void Join()
        {
            _InternalThread.Join();
        }

        #region Events

        private void OnJobStarted(object sender, JobEventArgs args)
        {
            JobStarted?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, JobEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
        }

        #endregion
    }
}
