#region

using System;
using System.Collections.Specialized;
using Controllers.Game;

#endregion

namespace Environment.Terrain
{
    public struct Block
    {
        #region STATIC

        #endregion

        public BitVector32 Bits;

        public ushort Id
        {
            get => (ushort) Bits[IdSection];
            set => Bits[IdSection] = (short) value;
        }

        /// <summary>
        ///     Determines whether the block is opaque.
        /// </summary>
        public bool Transparent
        {
            get => Bits[TransparencySection] == 0;
            set => Bits[TransparencySection] = value ? 0 : 1;
        }

        public static readonly BitVector32.Section IdSection;
        public static readonly BitVector32.Section TransparencySection;
        public static readonly BitVector32.Section[] Faces;

        static Block()
        {
            unchecked
            {
                IdSection = BitVector32.CreateSection(short.MaxValue);
            }

            TransparencySection = BitVector32.CreateSection(1, IdSection);
            Faces = new BitVector32.Section[6];
            Faces[0] = BitVector32.CreateSection(1, TransparencySection);
            Faces[1] = BitVector32.CreateSection(1, Faces[0]);
            Faces[2] = BitVector32.CreateSection(1, Faces[1]);
            Faces[3] = BitVector32.CreateSection(1, Faces[2]);
            Faces[4] = BitVector32.CreateSection(1, Faces[3]);
            Faces[5] = BitVector32.CreateSection(1, Faces[4]);
        }

        public Block(ushort id, byte faces = byte.MinValue)
        {
            Bits = new BitVector32(0)
            {
                [IdSection] = id,
                [Faces[0]] = faces & (byte) Direction.North,
                [Faces[1]] = faces & (byte) Direction.East,
                [Faces[2]] = faces & (byte) Direction.South,
                [Faces[3]] = faces & (byte) Direction.West,
                [Faces[4]] = faces & (byte) Direction.Up,
                [Faces[5]] = faces & (byte) Direction.Down
            };

            Transparent = BlockController.Current.IsBlockDefaultTransparent(Id);
        }

        public bool HasAnyFace()
        {
            if (Id == BlockController.BLOCK_EMPTY_ID)
            {
                return false;
            }

            return HasFace(Direction.North) || HasFace(Direction.East) || HasFace(Direction.South) ||
                   HasFace(Direction.East) || HasFace(Direction.Up) || HasFace(Direction.Down);
        }

        public bool HasFace(Direction direction)
        {
            switch (direction)
            {
                case Direction.None:
                    return false;
                case Direction.North:
                    return Bits[Faces[0]] == 1;
                case Direction.East:
                    return Bits[Faces[1]] == 1;
                case Direction.South:
                    return Bits[Faces[2]] == 1;
                case Direction.West:
                    return Bits[Faces[3]] == 1;
                case Direction.Up:
                    return Bits[Faces[4]] == 1;
                case Direction.Down:
                    return Bits[Faces[5]] == 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public void SetFace(Direction direction, bool value)
        {
            switch (direction)
            {
                case Direction.None:
                    return;
                case Direction.North:
                    Bits[Faces[0]] = value ? 1 : 0;
                    break;
                case Direction.East:
                    Bits[Faces[1]] = value ? 1 : 0;
                    break;
                case Direction.South:
                    Bits[Faces[2]] = value ? 1 : 0;
                    break;
                case Direction.West:
                    Bits[Faces[3]] = value ? 1 : 0;
                    break;
                case Direction.Up:
                    Bits[Faces[4]] = value ? 1 : 0;
                    break;
                case Direction.Down:
                    Bits[Faces[5]] = value ? 1 : 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}