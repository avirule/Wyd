#region

using System.Collections.Concurrent;

#endregion

namespace Wyd.System.Collections
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

        public T Retrieve() => _InternalCache.TryTake(out T item) ? item : default;

        public bool TryRetrieve(out T item) => _InternalCache.TryTake(out item);

        public void SetMaximumSize(int maximumSize)
        {
            MaximumSize = maximumSize;

            for (int iterations = _InternalCache.Count - MaximumSize; iterations > 0; iterations--)
            {
                _InternalCache.TryTake(out T _);
            }
        }
    }
}
