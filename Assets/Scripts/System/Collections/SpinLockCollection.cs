#region

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

#endregion

// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace Wyd.System.Collections
{
    internal static class Static
    {
        internal static TimeSpan MinimumWait = TimeSpan.FromMilliseconds(1);
    }

    public class SpinLockCollection<T>
    {
        private ConcurrentQueue<T> _Queue;
        private AutoResetEvent _AutoResetEvent;
        private CancellationToken _Token;

        public SpinLockCollection()
        {
            _Queue = new ConcurrentQueue<T>();
            _AutoResetEvent = new AutoResetEvent(false);
            _Token = CancellationToken.None;
        }

        public SpinLockCollection(CancellationToken token) : this() => _Token = token;

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

        public bool TryTake(out T result, TimeSpan timeout)
        {
            result = default;

            if (_Token.IsCancellationRequested)
            {
                return false;
            }

            if (_Queue.TryDequeue(out result))
            {
                return true;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (!_Token.IsCancellationRequested && (stopwatch.Elapsed < timeout))
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
