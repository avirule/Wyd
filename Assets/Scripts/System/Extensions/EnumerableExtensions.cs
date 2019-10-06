#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Wyd.System.Extensions
{
    public static class EnumerableExtensions
    {
        public static bool ContainsAll<T>(this IEnumerable<T> enumerable, IEnumerable<T> lookup) =>
            !lookup.Except(enumerable).Any();
    }
}
