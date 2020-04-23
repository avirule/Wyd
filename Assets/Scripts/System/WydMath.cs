#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Wyd.System.Extensions;

#endregion

namespace Wyd.System
{
    public static class WydMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 IndexTo3D(int index, int bounds)
        {
            int xQuotient = Math.DivRem(index, bounds, out int x);
            int zQuotient = Math.DivRem(xQuotient, bounds, out int z);
            int y = zQuotient % bounds;
            return new int3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 IndexTo3D(int index, int3 bounds)
        {
            int xQuotient = Math.DivRem(index, bounds.x, out int x);
            int zQuotient = Math.DivRem(xQuotient, bounds.z, out int z);
            int y = zQuotient % bounds.y;
            return new int3(x, y, z);
        }

        public static int FirstNonZeroComponent(int3 a)
        {
            for (int i = 0; i < 3; i++)
            {
                if (a[i] > 0)
                {
                    return a[i];
                }
            }

            throw new ArgumentException("No non-zero components.", nameof(a));
        }


        #region ToComponents()

        public static IEnumerable<int3> ToComponents(int3 a)
        {
            if (a.x != 0)
            {
                yield return new int3(a.x, 0, 0);
            }

            if (a.y != 0)
            {
                yield return new int3(0, a.y, 0);
            }

            if (a.z != 0)
            {
                yield return new int3(0, 0, a.z);
            }
        }

        public static IEnumerable<float3> ToComponents(float3 a)
        {
            if (a.x != 0f)
            {
                yield return new float3(a.x, 0, 0);
            }

            if (a.y != 0f)
            {
                yield return new float3(0, a.y, 0);
            }

            if (a.z != 0f)
            {
                yield return new float3(0, 0, a.z);
            }
        }

        #endregion


        #region ToFloat()

        public static float3 ToFloat(int3 a) => new float3(a.x, a.y, a.z);

        #endregion


        #region ToInt()

        public static int3 ToInt(float3 a) => new int3((int)a.x, (int)a.y, (int)a.z);

        #endregion


        #region RoundBy()

        public static float3 RoundBy(float3 a, float b) => math.floor(a / b) * b;
        public static float3 RoundBy(float3 a, float3 b) => math.floor(a / b) * b;

        #endregion


        #region Product()

        public static float Product(float3 a) => a.x * a.y * a.z;

        public static int Product(int3 a) => a.x * a.y * a.z;

        #endregion


        #region Bitwise / Bytes

        private static readonly byte[] _multiplyDeBruijnBitPosition =
        {
            0,
            1,
            28,
            2,
            29,
            14,
            24,
            3,
            30,
            22,
            20,
            15,
            25,
            17,
            4,
            8,
            31,
            27,
            13,
            23,
            21,
            19,
            16,
            7,
            26,
            12,
            18,
            6,
            11,
            5,
            10,
            9
        };

        public static byte CountSetBits(uint i)
        {
            i -= (i >> 1) & 0x55555555;
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (byte)((((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
        }

        public static bool ContainsAnyBits(this byte a, byte b) => (a & b) > 0;

        public static bool ContainsAllBits(this byte a, byte b) => (a & b) == b;

        public static int MostSigBitDigit(this int a) => _multiplyDeBruijnBitPosition[(a * 0x077CB531U) >> 27];
        public static int MostSigBitDigit(this uint a) => _multiplyDeBruijnBitPosition[(a * 0x077CB531U) >> 27];

        public static int LeastSigBitDigit(this byte a) =>
            _multiplyDeBruijnBitPosition[(uint)((a & -a) * 0x077CB531U) >> 27];

        public static int LeastSigBitDigit(this int a) =>
            _multiplyDeBruijnBitPosition[(uint)((a & -a) * 0x077CB531U) >> 27];

        public static byte SetBitByBoolWithMask(this byte a, byte mask, bool value) =>
            (byte)((a & ~mask) | (value.ToByte() << mask.LeastSigBitDigit()));

        public static int SetBitByBoolWithMask(this int a, int mask, bool value) =>
            (a & ~mask) | (value.ToByte() << mask.LeastSigBitDigit());

        #endregion
    }
}
