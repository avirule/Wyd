#region

using System.Collections.Concurrent;

#endregion

namespace Wyd.System.Collections
{
    public delegate void OnItemCulled<T>(ref T item);

    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _InternalCache;
        private readonly OnItemCulled<T> _ItemCulled;

        public int MaximumSize;

        public int Size => _InternalCache.Count;

        public ObjectPool(OnItemCulled<T> onItemCulled, int maximumSize = -1)
        {
            _InternalCache = new ConcurrentBag<T>();
            _ItemCulled = onItemCulled;

            MaximumSize = maximumSize;
        }

        public void CacheItem(T item)
        {
            // null check without boxing
            if (!(item is object))
            {
                return;
            }

            _InternalCache.Add(item);

            AttemptCullCache();
        }

        public T Retrieve() => _InternalCache.TryTake(out T item) ? item : default;

        public bool TryRetrieve(out T item) => _InternalCache.TryTake(out item);

        private void AttemptCullCache()
        {
            if (MaximumSize == -1)
            {
                return;
            }

            while (_InternalCache.Count > MaximumSize)
            {
                _InternalCache.TryTake(out T item); // null check without boxing
                _ItemCulled?.Invoke(ref item);
            }
        }
    }
}
