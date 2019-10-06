#region

using Wyd.Extensions;

#endregion

namespace Wyd
{
    /// <summary>
    ///     Byte math
    /// </summary>
    public static class Mathb
    {
        private static readonly byte[] MultiplyDeBruijnBitPosition =
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
            return (byte) ((((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
        }

        public static bool ContainsAnyBits(this byte a, byte b) => (a & b) > 0;

        public static bool ContainsAllBits(this byte a, byte b) => (a & b) == b;

        public static int MostSigBitDigit(this int a) => MultiplyDeBruijnBitPosition[(a * 0x077CB531U) >> 27];
        public static int MostSigBitDigit(this uint a) => MultiplyDeBruijnBitPosition[(a * 0x077CB531U) >> 27];

        public static int LeastSigBitDigit(this byte a) =>
            MultiplyDeBruijnBitPosition[(uint) ((a & -a) * 0x077CB531U) >> 27];

        public static int LeastSigBitDigit(this int a) =>
            MultiplyDeBruijnBitPosition[(uint) ((a & -a) * 0x077CB531U) >> 27];

        public static byte SetBitByBoolWithMask(this byte a, byte mask, bool value) =>
            (byte) ((a & ~mask) | (value.ToByte() << mask.LeastSigBitDigit()));

        public static int SetBitByBoolWithMask(this int a, int mask, bool value) =>
            (a & ~mask) | (value.ToByte() << mask.LeastSigBitDigit());
    }
}
