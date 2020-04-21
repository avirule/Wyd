#region

using System;
using System.Collections.Generic;
using Wyd.Controllers.World.Chunk;
using Wyd.Game.World;
using Wyd.Game.World.Blocks;

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

        public static IEnumerable<TEnum> GetEnumsList<TEnum>() where TEnum : Enum =>
            (TEnum[])Enum.GetValues(typeof(TEnum));

        public static bool HasState(this WorldState worldState, WorldState flag)
        {
            int intState = (int)worldState;
            int intFlag = (int)flag;

            return (intState & intFlag) > 0;
        }

        public static bool HasProperty(this BlockDefinition.Property property, BlockDefinition.Property flag)
        {
            int intProperty = (int)property;
            int intFlag = (int)flag;

            return (intProperty & intFlag) > 0;
        }
    }
}
