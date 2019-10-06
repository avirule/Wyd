#region

using System;
using System.Collections.Concurrent;

#endregion

namespace Wyd.Game
{
    public delegate ref T PreCachingOperation<T>(ref T item);

    public delegate void ItemCulledOperation<T>(ref T item);

    public class ObjectCache<T>
    {
        private readonly bool _CreateNewIfEmpty;
        private readonly ConcurrentStack<T> _InternalCache;
        private PreCachingOperation<T> _PreCachingOperation;
        private ItemCulledOperation<T> _ItemCulledOperation;

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

            if (createNewIfEmpty && (typeof(T).GetConstructor(Type.EmptyTypes) == null) && !typeof(T).IsValueType)
            {
                throw new ArgumentException(
                    $"Type T ({typeof(T)}) must have an empty constructor if `{nameof(_CreateNewIfEmpty)}` flag is true.",
                    nameof(_CreateNewIfEmpty));
            }

            _CreateNewIfEmpty = createNewIfEmpty;
            MaximumSize = maximumSize;

            if (preInitialize && (maximumSize > -1) && createNewIfEmpty)
            {
                for (int i = 0; i < maximumSize; i++)
                {
                    _InternalCache.Push(Activator.CreateInstance<T>());
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
                if (_CreateNewIfEmpty)
                {
                    item = Activator.CreateInstance<T>();
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
