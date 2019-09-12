#region

using System.Collections.Specialized;
using Controllers.State;

#endregion

namespace Game.World.Blocks
{
    public struct Block
    {
        #region Static Members

        public static readonly BitVector32.Section IdSection;
        public static readonly BitVector32.Section TransparencySection;
        public static readonly BitVector32.Section AllFacesSection;
        public static readonly BitVector32.Section[] FaceSections;
        public static readonly BitVector32.Section DamageSection;

        static Block()
        {
            IdSection = BitVector32.CreateSection(short.MaxValue);
            TransparencySection = BitVector32.CreateSection(1, IdSection);
            AllFacesSection = BitVector32.CreateSection(63, TransparencySection);
            FaceSections = new BitVector32.Section[6];
            FaceSections[0] = BitVector32.CreateSection(1, TransparencySection);
            FaceSections[1] = BitVector32.CreateSection(1, FaceSections[0]);
            FaceSections[2] = BitVector32.CreateSection(1, FaceSections[1]);
            FaceSections[3] = BitVector32.CreateSection(1, FaceSections[2]);
            FaceSections[4] = BitVector32.CreateSection(1, FaceSections[3]);
            FaceSections[5] = BitVector32.CreateSection(1, FaceSections[4]);
            DamageSection = BitVector32.CreateSection(15, FaceSections[5]);
        }

        #endregion

        private BitVector32 _Bits;

        public ushort Id
        {
            get => (ushort) _Bits[IdSection];
            private set => _Bits[IdSection] = (short) value;
        }

        /// <summary>
        ///     Determines whether the block is transparent.
        /// </summary>
        public bool Transparent
        {
            get => _Bits[TransparencySection] == 0;
            // value = true is transparent so that the default value of block is transparent
            set => _Bits[TransparencySection] = value ? 0 : 1;
        }

        public sbyte Damage
        {
            get => (sbyte) _Bits[DamageSection];
            set => _Bits[DamageSection] = value;
        }

        public Block(ushort id, sbyte faces = 0)
        {
            _Bits[IdSection] = id;
            _Bits[FaceSections[0]] = faces & (byte) Direction.North;
            _Bits[FaceSections[1]] = faces & (byte) Direction.East;
            _Bits[FaceSections[2]] = faces & (byte) Direction.South;
            _Bits[FaceSections[3]] = faces & (byte) Direction.West;
            _Bits[FaceSections[4]] = faces & (byte) Direction.Up;
            _Bits[FaceSections[5]] = faces & (byte) Direction.Down;
            _Bits[TransparencySection] = BlockController.Current.IsBlockDefaultTransparent(Id) ? 0 : 1;
        }

        public void Initialise(ushort id, sbyte faces = 0)
        {
            _Bits[IdSection] = id;
            _Bits[FaceSections[0]] = faces & (byte) Direction.North;
            _Bits[FaceSections[1]] = faces & (byte) Direction.East;
            _Bits[FaceSections[2]] = faces & (byte) Direction.South;
            _Bits[FaceSections[3]] = faces & (byte) Direction.West;
            _Bits[FaceSections[4]] = faces & (byte) Direction.Up;
            _Bits[FaceSections[5]] = faces & (byte) Direction.Down;
            _Bits[TransparencySection] = BlockController.Current.IsBlockDefaultTransparent(Id) ? 0 : 1;
        }

        public bool HasAnyFaces()
        {
            return _Bits[AllFacesSection] > 0;
        }

        public bool HasAllFaces()
        {
            return _Bits[AllFacesSection] == 63;
        }

        public bool HasFace(Direction direction)
        {
            return _Bits[FaceSections[((byte) direction).LeastSignificantBit()]] == 1;
        }

        public void SetFace(Direction direction, bool value)
        {
            _Bits[FaceSections[((byte) direction).LeastSignificantBit()]] = value ? 1 : 0;
        }

        public void ClearFaces()
        {
            _Bits[AllFacesSection] = 0;
        }
    }
}
