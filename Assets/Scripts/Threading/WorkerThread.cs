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
        
        public event EventHandler<WorkerThreadFinishedItemEventArgs> FinishedItem;

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
            if (!Running && _AbortToken.IsCancellationRequested)
            {
                return;
            }

            _AbortTokenSource.Cancel();
            WaitTimeout = 0;
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
            OnFinishedItem(this, new WorkerThreadFinishedItemEventArgs(_InternalThread.ManagedThreadId, threadedItem));
            
            Processing = false;
        }

        private void OnFinishedItem(object sender, WorkerThreadFinishedItemEventArgs args)
        {
            FinishedItem?.Invoke(sender, args);
        }
    }

    public class WorkerThreadFinishedItemEventArgs : EventArgs
    {
        public readonly int ManagedId;
        public readonly ThreadedItem ThreadedItem;

        public WorkerThreadFinishedItemEventArgs(int managedId, ThreadedItem threadedItem)
        {
            ManagedId = managedId;
            ThreadedItem = threadedItem;
        }
    }
}