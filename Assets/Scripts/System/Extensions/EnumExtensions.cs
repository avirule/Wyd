#region

using System;
using Wyd.Controllers.World.Chunk;

#endregion

namespace Wyd.System.Extensions
{
    public static class EnumExtensions
    {
        public static T Next<T>(this T src) where T : Enum
        {
            T[] arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf(arr, src) + 1;
            return arr.Length == j ? arr[0] : arr[j];
        }

        public static bool HasState(this State state, State flag)
        {
            int intState = (int)state;
            int intFlag = (int)flag;
            return (intState & intFlag) == intFlag;
        }
    }
}
