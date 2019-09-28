#region

using Extensions;

#endregion

/// <summary>
///     Byte math
/// </summary>
public static class Mathb
{
    private static readonly int[] MultiplyDeBruijnBitPosition =
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

    public static bool ContainsAnyBits(this byte a, byte b)
    {
        return (a & b) > 0;
    }

    public static bool ContainsAllBits(this byte a, byte b)
    {
        return (a & b) == b;
    }

    public static int LeastSigBitDigit(this byte a)
    {
        return MultiplyDeBruijnBitPosition[(uint) ((a & -a) * 0x077CB531U) >> 27];
    }

    public static byte SetBitByValueWithMask(this byte a, byte mask, bool value)
    {
        // avoids probable branch prediction slowdown on the cpu
        return (byte) ((a & ~mask) | (value.ToByte() << mask.LeastSigBitDigit()));
    }
}
