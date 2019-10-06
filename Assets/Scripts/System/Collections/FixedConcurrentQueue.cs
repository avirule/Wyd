#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#endregion

namespace Wyd.System.Collections
{
    public class FixedConcurrentQueue<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
    {
        private readonly ConcurrentQueue<T> _InternalQueue;
        private int _Count;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot =>
            throw new NotSupportedException(
                "The SyncRoot property may not be used for the synchronization of concurrent collections.");

        public readonly int MaximumSize;
        public int Count => _Count;
        public bool IsEmpty { get; private set; }

        public FixedConcurrentQueue(int maximumSize)
        {
            _InternalQueue = new ConcurrentQueue<T>();
            MaximumSize = maximumSize;
        }

        public FixedConcurrentQueue(IEnumerable<T> collection)
        {
            _InternalQueue = new ConcurrentQueue<T>(collection);
            MaximumSize = _Count = _InternalQueue.Count;
            IsEmpty = _InternalQueue.IsEmpty;
        }

        public void Enqueue(T item)
        {
            _InternalQueue.Enqueue(item);
            IsEmpty = false;

            if (Count == MaximumSize)
            {
                _InternalQueue.TryDequeue(out T _);
            }
            else
            {
                Interlocked.Increment(ref _Count);
            }
        }

        private bool TryDequeue(out T item) => _InternalQueue.TryDequeue(out item);

        public bool TryPeek(out T item) => _InternalQueue.TryPeek(out item);

        public IEnumerator<T> GetEnumerator() => _InternalQueue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T[] ToArray() => _InternalQueue.ToArray();

        bool IProducerConsumerCollection<T>.TryAdd(T item)
        {
            Enqueue(item);

            return true;
        }

        bool IProducerConsumerCollection<T>.TryTake(out T item) => TryDequeue(out item);

        void ICollection.CopyTo(Array array, int index)
        {
            if (array is T[] arrayAs)
            {
                _InternalQueue.CopyTo(arrayAs, index);
            }
            else
            {
                throw new ArgumentNullException(nameof(array));
            }
        }

        public void CopyTo(T[] array, int index)
        {
            _InternalQueue.CopyTo(array, index);
        }
    }
}
