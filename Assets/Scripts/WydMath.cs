#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Mathematics;
using Wyd.Extensions;

#endregion

namespace Wyd
{
    public static class WydMath
    {
        public static int Wrap(int v, int delta, int minVal, int maxVal)
        {
            int mod = (maxVal + 1) - minVal;
            v += delta - minVal;
            v += (1 - (v / mod)) * mod;
            return (v % mod) + minVal;
        }

        public static int PointToIndex(int2 a, int size) => a.x + (size * a.y);
        public static int PointToIndex(int3 a, int size) => a.x + (size * (a.z + (size * a.y)));
        public static int PointToIndex(float3 a, int size) => (int)(a.z + (size * (a.z + (size * a.y))));
        public static int PointToIndex(int x, int y, int z, int size) => x + (size * (z + (size * y)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 IndexTo2D(int index, int bounds)
        {
            int xQuotient = Math.DivRem(index, bounds, out int x);
            int y = xQuotient % bounds;
            return new int2(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 IndexTo3D(int index, int bounds)
        {
            int xQuotient = Math.DivRem(index, bounds, out int x);
            int zQuotient = Math.DivRem(xQuotient, bounds, out int z);
            int y = zQuotient % bounds;
            return new int3(x, y, z);
        }

        public static void IndexTo3D(int index, int bounds, out int x, out int y, out int z)
        {
            int xQuotient = Math.DivRem(index, bounds, out x);
            int zQuotient = Math.DivRem(xQuotient, bounds, out z);
            y = zQuotient % bounds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 IndexTo3D(int index, int3 bounds)
        {
            int xQuotient = Math.DivRem(index, bounds.x, out int x);
            int zQuotient = Math.DivRem(xQuotient, bounds.z, out int z);
            int y = zQuotient % bounds.y;
            return new int3(x, y, z);
        }

        public static int FirstNonZeroIndex(int3 a)
        {
            if (a.x != 0)
            {
                return 0;
            }
            else if (a.y != 0)
            {
                return 1;
            }
            else if (a.z != 0)
            {
                return 2;
            }

            return -1;
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
        public static float4 ToFloat(int4 a) => new float4(a.x, a.y, a.z, a.w);

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

        public static byte[] ObjectToByteArray(object obj)
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                binaryFormatter.Serialize(memoryStream, obj);
                return memoryStream.ToArray();
            }
        }

        private static readonly byte[] _MultiplyDeBruijnBitPosition =
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

        public static int MostSigBitDigit(this int a) => _MultiplyDeBruijnBitPosition[(a * 0x077CB531U) >> 27];
        public static int MostSigBitDigit(this uint a) => _MultiplyDeBruijnBitPosition[(a * 0x077CB531U) >> 27];

        public static int LeastSigBitDigit(this byte a) =>
            _MultiplyDeBruijnBitPosition[(uint)((a & -a) * 0x077CB531U) >> 27];

        public static int LeastSigBitDigit(this int a) =>
            _MultiplyDeBruijnBitPosition[(uint)((a & -a) * 0x077CB531U) >> 27];

        public static byte SetBitByBoolWithMask(this byte a, byte mask, bool value) =>
            (byte)((a & ~mask) | (value.ToByte() << mask.LeastSigBitDigit()));

        public static int SetBitByBoolWithMask(this int a, int mask, bool value) =>
            (a & ~mask) | (value.ToByte() << mask.LeastSigBitDigit());

        #endregion
    }
}
