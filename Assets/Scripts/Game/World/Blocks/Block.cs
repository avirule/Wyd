#region

using System.Collections.Specialized;
using Controllers.Game;

#endregion

namespace Game.World.Blocks
{
    public struct Block
    {
        public BitVector32 Bits;

        public ushort Id
        {
            get => (ushort) Bits[IdSection];
            set => Bits[IdSection] = (short) value;
        }

        /// <summary>
        ///     Determines whether the block is transparent.
        /// </summary>
        public bool Transparent
        {
            get => Bits[TransparencySection] == 0;
            set => Bits[TransparencySection] = value ? 0 : 1;
        }

        public static readonly BitVector32.Section IdSection;
        public static readonly BitVector32.Section TransparencySection;
        public static readonly BitVector32.Section[] FaceSections;

        static Block()
        {
            unchecked
            {
                IdSection = BitVector32.CreateSection(short.MaxValue);
            }

            TransparencySection = BitVector32.CreateSection(1, IdSection);
            FaceSections = new BitVector32.Section[6];
            FaceSections[0] = BitVector32.CreateSection(1, TransparencySection);
            FaceSections[1] = BitVector32.CreateSection(1, FaceSections[0]);
            FaceSections[2] = BitVector32.CreateSection(1, FaceSections[1]);
            FaceSections[3] = BitVector32.CreateSection(1, FaceSections[2]);
            FaceSections[4] = BitVector32.CreateSection(1, FaceSections[3]);
            FaceSections[5] = BitVector32.CreateSection(1, FaceSections[4]);
        }

        public Block(ushort id, byte faces = byte.MinValue)
        {
            Initialise(id, faces);
        }

        public void Initialise(ushort id, byte faces = byte.MinValue)
        {
            Bits = new BitVector32(0)
            {
                [IdSection] = id,
                [FaceSections[0]] = faces & (byte) Direction.North,
                [FaceSections[1]] = faces & (byte) Direction.East,
                [FaceSections[2]] = faces & (byte) Direction.South,
                [FaceSections[3]] = faces & (byte) Direction.West,
                [FaceSections[4]] = faces & (byte) Direction.Up,
                [FaceSections[5]] = faces & (byte) Direction.Down
            };

            Transparent = BlockController.Current.IsBlockDefaultTransparent(Id);
        }

        public bool HasAnyFace()
        {
            return HasFace(Direction.North) || HasFace(Direction.East) || HasFace(Direction.South) ||
                   HasFace(Direction.East) || HasFace(Direction.Up) || HasFace(Direction.Down);
        }

        public bool HasAllFaces()
        {
            return HasFace(Direction.North) && HasFace(Direction.East) && HasFace(Direction.South) &&
                   HasFace(Direction.East) && HasFace(Direction.Up) && HasFace(Direction.Down);
        }

        public bool HasFace(Direction direction)
        {
            return Bits[FaceSections[((byte) direction).SmallestBitDigit()]] == 1;
        }

        public void SetFace(Direction direction, bool value)
        {
            Bits[FaceSections[((byte) direction).SmallestBitDigit()]] = value ? 1 : 0;
        }

        public void ClearFaces()
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int face = 0; face < FaceSections.Length; face++)
            {
                if (Bits[FaceSections[face]] == 1)
                {
                    Bits[FaceSections[face]] = 0;
                }
            }
        }
    }
}