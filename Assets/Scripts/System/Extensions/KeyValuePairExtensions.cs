#region

using System.Collections.Generic;

#endregion

namespace Wyd.System.Extensions
{
    public static class KeyValuePairExtensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value) =>
            (key, value) = (kvp.Key, kvp.Value);
    }
}
