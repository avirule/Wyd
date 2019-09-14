#region

using System;
using System.Collections.Concurrent;

#endregion

namespace Game
{
    public class ObjectCache<T> where T : new()
    {
        private readonly ConcurrentStack<T> _InternalCache;
        private Func<T, T> _PreCachingOperation;
        private Action<T> _ItemCulledOperation;

        public bool CreateNewIfEmpty;
        public int MaximumSize;

        public int Size => _InternalCache.Count;

        public ObjectCache(
            Func<T, T> preCachingOperation = default, Action<T> itemCulledOperation = default,
            bool createNewIfEmpty = false, int maximumSize = -1)
        {
            _InternalCache = new ConcurrentStack<T>();
            SetPreCachingOperation(preCachingOperation);
            SetItemCulledOperation(itemCulledOperation);
            CreateNewIfEmpty = createNewIfEmpty;
            MaximumSize = maximumSize;
        }

        public void SetPreCachingOperation(Func<T, T> preCachingOperation)
        {
            if (preCachingOperation == default)
            {
                return;
            }

            _PreCachingOperation = preCachingOperation;
        }

        public void SetItemCulledOperation(Action<T> itemCulledOperation)
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
                item = _PreCachingOperation(item);
            }

            if (item == null)
            {
                return;
            }

            _InternalCache.Push(item);

            if (MaximumSize > -1)
            {
                CullCache();
            }
        }

        public T RetrieveItem()
        {
            if ((_InternalCache.Count == 0) || !_InternalCache.TryPop(out T lastItem))
            {
                return CreateNewIfEmpty ? new T() : default;
            }


            return lastItem;
        }

        private void CullCache()
        {
            if (MaximumSize == -1)
            {
                return;
            }

            while (_InternalCache.Count > MaximumSize)
            {
                if (_InternalCache.TryPop(out T lastItem))
                {
                    _ItemCulledOperation(lastItem);
                }
            }
        }
    }
}
