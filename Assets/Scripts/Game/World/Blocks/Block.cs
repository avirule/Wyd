#region

using Controllers.State;

#endregion

namespace Game.World.Blocks
{
    public struct Block
    {
        public enum Types
        {
            None,

            // this basically means that
            // it's placed in the first
            // terrain step
            Raw,
            Ore
        }

        public const byte TRANSPARENCY_MASK = 0b0100_0000;
        public const byte FACES_MASK = 0b0011_1111;

        /// <summary>
        ///     Determines whether the block is transparent.
        /// </summary>
        public bool Transparent
        {
            get => !Faces.MatchesAny(TRANSPARENCY_MASK);
            // value = true is transparent so that the default value of block is transparent
            private set => SetTransparency(value);
        }

        public ushort Id { get; private set; }

        public byte Faces { get; private set; }
        //public sbyte Damage { get; private set; }

        public Block(ushort id, byte faces = 0)
        {
            Id = id;
            Faces = faces;
            //Damage = 0;
        }

        public void Initialise(ushort id, byte faces = 0)
        {
            Id = id;
            Faces = faces;
            Transparent = BlockController.Current.GetBlockRule(Id)?.Transparent ?? true;
        }

        public bool HasAnyFaces()
        {
            return Faces.MatchesAny(FACES_MASK);
        }

        public bool HasAllFaces()
        {
            return Faces.MatchesAll(FACES_MASK);
        }

        public bool HasFace(Direction direction)
        {
            return Faces.MatchesAny((byte) direction);
        }

        public void SetFace(Direction direction, bool value)
        {
            Faces = Faces.SetBitByValueWithMask((byte) direction, value);
        }

        public void SetTransparency(bool transparent)
        {
            Faces = Faces.SetBitByValueWithMask(TRANSPARENCY_MASK, !transparent);
        }

        public void ClearFaces()
        {
            Faces &= FACES_MASK ^ byte.MaxValue;
        }
    }
}
