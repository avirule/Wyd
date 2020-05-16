#region

using System;
using System.Collections.Concurrent;

#endregion

namespace Wyd.Collections
{
    public delegate void OnItemCulled<T>(ref T item);

    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _InternalCache;

        public int MaximumSize { get; private set; }

        public int Size => _InternalCache.Count;

        public ObjectPool(int maximumSize = -1)
        {
            _InternalCache = new ConcurrentBag<T>();

            MaximumSize = maximumSize;
        }

        public bool TryAdd(T item)
        {
            // null check without boxing
            if (!(item is object) || ((MaximumSize > -1) && (_InternalCache.Count > MaximumSize)))
            {
                return false;
            }

            _InternalCache.Add(item);
            return true;
        }

        public bool TryTake(out T item) => _InternalCache.TryTake(out item);

        public void SetMaximumSize(int maximumSize)
        {
            MaximumSize = maximumSize;

            for (int iterations = _InternalCache.Count - MaximumSize; iterations > 0; iterations--)
            {
                _InternalCache.TryTake(out T item);
                ItemCulled?.Invoke(this, item);
            }
        }

        /// <summary>
        ///     Event fired when an item is thrown away due to exceeding maximum size.
        /// </summary>
        public event EventHandler<T> ItemCulled;
    }
}
