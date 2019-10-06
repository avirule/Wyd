namespace Wyd
{
    public class BitVector64
    {
        public struct Section
        {
            internal Section(short mask, short offset)
            {
                Mask = mask;
                Offset = offset;
            }

            public short Mask { get; }
            public short Offset { get; }

            public override bool Equals(object obj)
            {
                if (obj is Section sectionObj)
                {
                    return Equals(sectionObj);
                }

                return false;
            }

            public bool Equals(Section obj)
            {
                if (obj.Mask == Mask)
                {
                    return obj.Offset == Offset;
                }

                return false;
            }


            public override int GetHashCode() => base.GetHashCode();
        }

        private ulong _Data;

        public BitVector64(long data) => _Data = (ulong) data;

        public BitVector64(BitVector64 value) => _Data = value._Data;

        public bool this[long bit]
        {
            get => ((long) _Data & bit) == bit;
            set
            {
                if (value)
                {
                    _Data |= (ulong) bit;
                }
                else
                {
                    _Data &= (ulong) ~bit;
                }
            }
        }

        public long this[Section section]
        {
            get => (long) ((_Data & (ulong) section.Mask) << section.Offset) >> section.Offset;
            set
            {
                value <<= section.Offset;
                int num = (ushort.MaxValue & section.Mask) << section.Offset;
                _Data = (ulong) (((long) _Data & ~num) | (value & num));
            }
        }
    }
}
