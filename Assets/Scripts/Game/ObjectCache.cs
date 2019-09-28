#region

using System.Collections.Concurrent;

#endregion

namespace Game
{
    public delegate ref T PreCachingOperation<T>(ref T item);

    public delegate void ItemCulledOperation<T>(ref T item);

    public class ObjectCache<T> where T : new()
    {
        private readonly ConcurrentStack<T> _InternalCache;
        private PreCachingOperation<T> _PreCachingOperation;
        private ItemCulledOperation<T> _ItemCulledOperation;

        public bool CreateNewIfEmpty;
        public int MaximumSize;

        public int Size => _InternalCache.Count;

        public ObjectCache(
            bool createNewIfEmpty, bool preInitialize = false, int maximumSize = -1,
            PreCachingOperation<T> preCachingOperation = default,
            ItemCulledOperation<T> itemCulledOperation = default)
        {
            _InternalCache = new ConcurrentStack<T>();
            SetPreCachingOperation(preCachingOperation);
            SetItemCulledOperation(itemCulledOperation);
            CreateNewIfEmpty = createNewIfEmpty;
            MaximumSize = maximumSize;

            if (preInitialize && (maximumSize > -1))
            {
                for (int i = 0; i < maximumSize; i++)
                {
                    _InternalCache.Push(new T());
                }
            }
        }

        public void SetPreCachingOperation(PreCachingOperation<T> preCachingOperation)
        {
            if (preCachingOperation == default)
            {
                return;
            }

            _PreCachingOperation = preCachingOperation;
        }

        public void SetItemCulledOperation(ItemCulledOperation<T> itemCulledOperation)
        {
            if (itemCulledOperation == default)
            {
                return;
            }

            _ItemCulledOperation = itemCulledOperation;
        }

        public void CacheItem(ref T item)
        {
            if (_PreCachingOperation != default)
            {
                item = ref _PreCachingOperation(ref item);
            }

            if (!(item is object))
            {
                return;
            }

            _InternalCache.Push(item);

            if (MaximumSize > -1)
            {
                AttemptCullCache();
            }
        }

        public bool TryRetrieveItem(out T item)
        {
            if ((_InternalCache.Count == 0)
                || !_InternalCache.TryPop(out item)
                || !(item is object))
            {
                if (CreateNewIfEmpty)
                {
                    item = new T();
                }
                else
                {
                    item = default;
                    return false;
                }
            }

            return true;
        }

        private void AttemptCullCache()
        {
            if (MaximumSize == -1)
            {
                return;
            }

            while (_InternalCache.Count > MaximumSize)
            {
                if (_InternalCache.TryPop(out T lastItem)
                    && lastItem is object) // null check without boxing
                {
                    _ItemCulledOperation?.Invoke(ref lastItem);
                }
            }
        }
    }
}
