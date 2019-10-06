#region

using System;

#endregion

namespace Wyd
{
    public struct BitVectorU32
    {
        public struct Section
        {
            public readonly uint Mask;
            public readonly byte Offset;
            public readonly uint Overflow;

            public Section(uint mask, byte offset, uint overflow)
            {
                if (offset > 32)
                {
                    offset = 32;
                }

                (Mask, Offset, Overflow) = (mask, offset, overflow);
            }
        }

        public uint Data;

        public uint this[Section section]
        {
            get => (Data >> section.Offset) & section.Mask;
            set => Data |= (value << section.Offset) & section.Mask;
        }

        public Section CreateSection(uint maxValue, byte offset = 0)
        {
            if (maxValue < 1)
            {
                throw new ArgumentException("Argument must be >=1.", nameof(maxValue));
            }


            if (offset > 31)
            {
                throw new ArgumentException("Argument must be <=31", nameof(offset));
            }

            // did this the dumb way
            int remainingShift = 32 - maxValue.MostSigBitDigit();
            uint finalMask = (uint.MaxValue << remainingShift) >> (remainingShift + offset);

            return new Section(finalMask, offset, finalMask - maxValue);
        }

        public int CountSetBits() => Mathb.CountSetBits(Data);
    }
}
