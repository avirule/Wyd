#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace Threading
{
    public class SingleThreadedQueue : IDisposable
    {
        private readonly Thread _ProcessingThread;
        private readonly BlockingCollection<ThreadedItem> _ProcessQueue;
        private readonly List<ThreadedItem> _FinishedItems;
        private readonly CancellationTokenSource _AbortTokenSource;
        private CancellationToken _AbortToken;

        public bool Disposed { get; private set; }
        public bool Running { get; private set; }

        public int MillisecondWaitTimeout;

        public SingleThreadedQueue(int millisecondWaitTimeout)
        {
            MillisecondWaitTimeout = millisecondWaitTimeout;
            _ProcessingThread = new Thread(ProcessThreadedItems);
            _ProcessQueue = new BlockingCollection<ThreadedItem>();
            _FinishedItems = new List<ThreadedItem>();
            _AbortTokenSource = new CancellationTokenSource();
            _AbortToken = _AbortTokenSource.Token;

            Running = false;
        }

        public void Start()
        {
            _ProcessingThread.Start();
            Running = true;
        }

        public void Abort()
        {
            _AbortTokenSource.Cancel();
            MillisecondWaitTimeout = 0;
            Running = false;
        }

        private void ProcessThreadedItems()
        {
            while (!_AbortToken.IsCancellationRequested)
            {
                ThreadedItem threadedItem = default;

                try
                {
                    if (!_ProcessQueue.TryTake(out threadedItem, MillisecondWaitTimeout, _AbortToken))
                    {
                        continue;
                    }

                    if (threadedItem == default)
                    {
                        continue;
                    }

                    threadedItem.Execute();
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                _FinishedItems.Add(threadedItem);
            }

            Dispose();
        }

        /// <summary>
        ///     Adds specified ThreadedItem to queue and returns a unique identity
        /// </summary>
        /// <param name="threadedItem"></param>
        /// <returns>unique object identity</returns>
        public object AddThreadedItem(ThreadedItem threadedItem)
        {
            if (!Running)
            {
                return null;
            }

            string guid = Guid.NewGuid().ToString();
            threadedItem.Identity = guid;
            _ProcessQueue.Add(threadedItem, _AbortToken);

            return guid;
        }

        public bool TryGetFinishedItem(object identity, out ThreadedItem threadedItem)
        {
            for (int i = _FinishedItems.Count - 1; i >= 0; i--)
            {
                if ((i >= _FinishedItems.Count) ||
                    (_FinishedItems[i] == default) ||
                    (_FinishedItems[i].Identity != identity) ||
                    !_FinishedItems[i].IsDone)
                {
                    continue;
                }

                threadedItem = _FinishedItems[i];
                _FinishedItems.RemoveAt(i);
                return true;
            }

            threadedItem = default;
            return false;
        }

        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            _ProcessQueue?.Dispose();
            Disposed = true;
        }
    }
}