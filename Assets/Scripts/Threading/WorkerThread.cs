#region

using System;
using System.Collections.Concurrent;
using System.Threading;

#endregion

namespace Threading
{
    public class WorkerThread
    {
        private readonly Thread _InternalThread;
        private readonly BlockingCollection<ThreadedItem> _ItemQueue;
        private readonly CancellationTokenSource _AbortTokenSource;
        private readonly CancellationToken _AbortToken;

        public int WaitTimeout;

        public bool Running { get; private set; }
        public bool Processing { get; private set; }
        public int ManagedThreadId => _InternalThread.ManagedThreadId;

        public event EventHandler<ThreadedItemFinishedEventArgs> ThreadedItemFinished;

        public WorkerThread(int waitTimeout)
        {
            _InternalThread = new Thread(ProcessItemQueue);
            _ItemQueue = new BlockingCollection<ThreadedItem>();
            _AbortTokenSource = new CancellationTokenSource();
            _AbortToken = _AbortTokenSource.Token;

            WaitTimeout = waitTimeout;
        }

        public void Start()
        {
            _InternalThread.Start();
            Running = true;
        }

        public void Abort()
        {
            _AbortTokenSource.Cancel();
            WaitTimeout = 1;
            Running = false;
        }

        public bool QueueThreadedItem(ThreadedItem threadedItem)
        {
            return _ItemQueue.TryAdd(threadedItem);
        }

        private void ProcessItemQueue()
        {
            while (!_AbortToken.IsCancellationRequested)
            {
                try
                {
                    if (_ItemQueue.TryTake(out ThreadedItem threadedItem, WaitTimeout, _AbortToken))
                    {
                        ProcessThreadedItem(threadedItem);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Thread aborted
                    return;
                }
            }
        }

        private void ProcessThreadedItem(ThreadedItem threadedItem)
        {
            Processing = true;

            threadedItem.Execute();
            OnThreadedItemFinished(this, new ThreadedItemFinishedEventArgs(threadedItem));

            Processing = false;
        }

        private void OnThreadedItemFinished(object sender, ThreadedItemFinishedEventArgs args)
        {
            ThreadedItemFinished?.Invoke(sender, args);
        }
    }
}