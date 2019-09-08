#region

using Controllers.Game;

#endregion

namespace Game.World.Blocks
{
    public struct Block
    {
        private const sbyte _TRANSPARENCY_MASK = 0b01000000;

        public ushort ID { get; private set; }

        /// <summary>
        ///     Determines whether the block is transparent.
        /// </summary>
        public bool Transparent
        {
            // set to true == 0 so that the default value is true.
            get => (Faces & _TRANSPARENCY_MASK) == 0;
            set
            {
                if (value)
                {
                    Faces &= ~_TRANSPARENCY_MASK;
                }
                else
                {
                    Faces |= _TRANSPARENCY_MASK;
                }
            }
        }

        public sbyte Faces { get; set; }

        public Block(ushort id, sbyte faces = 0)
        {
            ID = id;
            Faces = faces;
            Transparent = BlockController.Current.IsBlockDefaultTransparent(ID);
        }

        public void Initialise(ushort id, sbyte faces = 0)
        {
            ID = id;
            Faces = faces;
            Transparent = BlockController.Current.IsBlockDefaultTransparent(ID);
        }

        public bool HasAnyFace()
        {
            return (Faces & 0) != 0;
        }

        public bool HasAllFaces()
        {
            // if it is greater than this byte, then 6 or more bits
            // have been set, so all faces are true
            return (Faces & (sbyte) Direction.All) == (sbyte) Direction.All;
        }

        public bool HasFace(Direction direction)
        {
            return (Faces & (byte) direction) != 0;
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
            Faces = 0;
        }
    }
}