#region

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

#endregion

namespace Wyd.System.Collections
{
    internal static class Static
    {
        internal static TimeSpan MinimumWait = TimeSpan.FromMilliseconds(1);
    }

    public class SpinLockCollection<T>
    {
        private readonly ConcurrentQueue<T> _Queue;
        private readonly AutoResetEvent _AutoResetEvent;

        public SpinLockCollection()
        {
            _Queue = new ConcurrentQueue<T>();
            _AutoResetEvent = new AutoResetEvent(false);
        }

        public void Add(T item)
        {
            _Queue.Enqueue(item);
            _AutoResetEvent.Set();
        }

        public bool TryPeek(out T result) => _Queue.TryPeek(out result);

        public T Take()
        {
            T item;

            while (!_Queue.TryDequeue(out item))
            {
                _AutoResetEvent.WaitOne();
            }

            return item;
        }

        public bool TryTake(out T result, TimeSpan timeout, CancellationToken token)
        {
            result = default;

            if (token.IsCancellationRequested)
            {
                return false;
            }

            if (_Queue.TryDequeue(out result))
            {
                return true;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (!token.IsCancellationRequested && (stopwatch.Elapsed < timeout))
            {
                if (_Queue.TryDequeue(out result))
                {
                    return true;
                }

                TimeSpan remainingTimeout = timeout - stopwatch.Elapsed;

                if (remainingTimeout <= TimeSpan.Zero)
                {
                    break;
                }

                if (remainingTimeout < Static.MinimumWait)
                {
                    remainingTimeout = Static.MinimumWait;
                }

                _AutoResetEvent.WaitOne(remainingTimeout);
            }

            return false;
        }
    }
}
