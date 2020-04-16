#region

using System;

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

        public static bool UncheckedHasFlag(this Enum value, Enum flag)
        {
            int intValue = Convert.ToInt32(value);
            int intFlag = Convert.ToInt32(flag);
            return (intValue & intFlag) == intFlag;
        }
    }
}
