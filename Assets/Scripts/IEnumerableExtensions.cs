#region

using System.Collections.Generic;
using System.Linq;

#endregion

public static class IEnumerableExtensions
{
    public static bool ContainsAll<T>(this IEnumerable<T> enumerable, IEnumerable<T> lookup)
    {
        return !lookup.Except(enumerable).Any();
    }
}
