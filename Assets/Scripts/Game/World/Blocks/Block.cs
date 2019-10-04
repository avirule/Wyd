#region

using Controllers.State;
using UnityEditor.U2D;

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

        public const byte FACES_MASK = 0b0011_1111;
        public const byte TRANSPARENCY_MASK = 0b0100_0000;
        public const byte DAMAGE_MASK = 0b0000_1111;
        public const byte LIGHT_LEVEL_MASK = 0b1111_0000;

        private byte _DamageLightLevel;
        
        /// <summary>
        ///     Determines whether the block is transparent.
        /// </summary>
        public bool Transparent
        {
            get => !Faces.ContainsAnyBits(TRANSPARENCY_MASK);
            // value = true is transparent so that the default value of block is transparent
            private set => SetTransparency(value);
        }

        public ushort Id { get; private set; }

        public byte Faces { get; private set; }
        public byte Damage
        {
            get => (byte)(DAMAGE_MASK & _DamageLightLevel);
            private set => _DamageLightLevel |= (byte)(value & DAMAGE_MASK);
        }
        
        public byte LightLevel
        {
            get => (byte)((_DamageLightLevel & LIGHT_LEVEL_MASK) >> 4);
            set => _DamageLightLevel |= (byte)((value << 4) & LIGHT_LEVEL_MASK);
        }

        public Block(ushort id, byte faces = 0, byte damage = 0)
        {
            Id = id;
            Faces = faces;
            _DamageLightLevel = damage;
        }

        public void Initialise(ushort id, byte faces = 0, byte damage = 0)
        {
            Id = id;
            Faces = faces;
            Damage = damage;

            if (BlockController.Current.TryGetBlockRule(id, out IReadOnlyBlockRule blockRule))
            {
                Transparent = blockRule.Transparent;
                LightLevel = blockRule.LightLevel;
            }
        }

        public bool HasAnyFaces() => Faces.ContainsAnyBits(FACES_MASK);

        public bool HasAllFaces() => Faces.ContainsAllBits(FACES_MASK);

        public bool HasFace(Direction direction) => Faces.ContainsAnyBits((byte) direction);

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
