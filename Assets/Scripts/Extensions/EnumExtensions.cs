#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Wyd.World;
using Wyd.World.Blocks;

#endregion

namespace Wyd.Extensions
{
    public static class EnumExtensions
    {
        public static T Next<T>(this T src) where T : Enum
        {
            T[] arr = (T[])Enum.GetValues(src.GetType());
            int j = Array.IndexOf(arr, src) + 1;
            return arr.Length == j ? arr[0] : arr[j];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<TEnum> GetEnumsList<TEnum>() where TEnum : Enum =>
            (TEnum[])Enum.GetValues(typeof(TEnum));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasState(this WorldState worldState, WorldState flag)
        {
            int intState = (int)worldState;
            int intFlag = (int)flag;

            return (intState & intFlag) == intFlag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasProperty(this BlockDefinition.Property property, BlockDefinition.Property flag)
        {
            int intProperty = (int)property;
            int intFlag = (int)flag;

            return (intProperty & intFlag) == intFlag;
        }

        public static string GetAlias(this FullScreenMode fullScreenMode)
        {
            switch (fullScreenMode)
            {
                case FullScreenMode.ExclusiveFullScreen:
                    return "Fullscreen";
                case FullScreenMode.FullScreenWindow:
                    return "Borderless Windowed";
                case FullScreenMode.MaximizedWindow:
                case FullScreenMode.Windowed:
                    return "Windowed";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fullScreenMode), fullScreenMode, null);
            }
        }
    }
}
