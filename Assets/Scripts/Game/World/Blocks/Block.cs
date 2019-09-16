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

        public const sbyte TRANSPARENCY_MASK = 0b0100_0000;
        public const sbyte FACES_MASK = 0b0011_1111;

        /// <summary>
        ///     Determines whether the block is transparent.
        /// </summary>
        public bool Transparent
        {
            get => (Faces & TRANSPARENCY_MASK) == 0;
            // value = true is transparent so that the default value of block is transparent
            private set => SetTransparency(value);
        }

        public ushort Id { get; private set; }
        public sbyte Faces { get; private set; }
        public sbyte Damage { get; private set; }

        public Block(ushort id, sbyte faces = 0)
        {
            Id = id;
            Faces = faces;
            Damage = 0;
        }

        public void Initialise(ushort id, sbyte faces = 0)
        {
            Id = id;
            Faces = faces;
            Transparent = BlockController.Current.IsBlockDefaultTransparent(Id);
        }

        public void SetTransparency(bool transparent)
        {
            if (transparent)
            {
                Faces &= ~TRANSPARENCY_MASK;
            }
            else
            {
                Faces |= TRANSPARENCY_MASK;
            }
        }

        public bool HasAnyFaces()
        {
            return (Faces & FACES_MASK) > 0;
        }

        public bool HasAllFaces()
        {
            return (Faces & FACES_MASK) == FACES_MASK;
        }

        public bool HasFace(Direction direction)
        {
            return (Faces & (sbyte) direction) > 0;
        }

        public void SetFace(Direction direction, bool value)
        {
            if (value)
            {
                Faces |= (sbyte) direction;
            }
            else
            {
                // for the record, this is stupid.
                Faces &= (sbyte) ~(sbyte) direction;
            }
        }

        public void ClearFaces()
        {
            Faces &= ~FACES_MASK;
        }
    }
}
