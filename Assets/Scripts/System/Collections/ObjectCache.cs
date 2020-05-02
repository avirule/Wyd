#region

using System;
using System.Collections.Concurrent;

#endregion

namespace Wyd.System.Collections
{
    public delegate T PreCachingOperation<T>(T item);

    public delegate void ItemCulledOperation<T>(ref T item);

    public class ObjectCache<T>
    {
        private readonly ConcurrentStack<T> _InternalCache;
        private PreCachingOperation<T> _PreCachingOperation;
        private ItemCulledOperation<T> _ItemCulledOperation;

        public int MaximumSize;

        public int Size => _InternalCache.Count;

        public ObjectCache(bool preInitialize = false, int maximumSize = -1,
            PreCachingOperation<T> preCachingOperation = null,
            ItemCulledOperation<T> itemCulledOperation = null)
        {
            _InternalCache = new ConcurrentStack<T>();
            SetPreCachingOperation(preCachingOperation);
            SetItemCulledOperation(itemCulledOperation);

            MaximumSize = maximumSize;

            if (!preInitialize || (maximumSize <= -1))
            {
                return;
            }

            for (int i = 0; i < maximumSize; i++)
            {
                _InternalCache.Push(Activator.CreateInstance<T>());
            }
        }

        public void SetPreCachingOperation(PreCachingOperation<T> preCachingOperation)
        {
            if (preCachingOperation == null)
            {
                return;
            }

            _PreCachingOperation = preCachingOperation;
        }

        public void SetItemCulledOperation(ItemCulledOperation<T> itemCulledOperation)
        {
            if (itemCulledOperation == null)
            {
                return;
            }

            _ItemCulledOperation = itemCulledOperation;
        }

        public void CacheItem(T item)
        {
            if (_PreCachingOperation != null)
            {
                item = _PreCachingOperation(item);
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

        public T Retrieve()
        {
            if ((_InternalCache.Count == 0)
                || !_InternalCache.TryPop(out T item)
                || !(item is object))
            {
                return default;
            }

            return item;
        }

        public bool TryRetrieve(out T item)
        {
            if ((_InternalCache.Count != 0)
                && _InternalCache.TryPop(out item)
                && item is object)
            {
                return true;
            }

            item = default;
            return false;
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
