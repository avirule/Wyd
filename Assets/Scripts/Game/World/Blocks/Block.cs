#region

using Wyd.Controllers.State;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Blocks
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

        private const int ID_MASK = 0b000_0000_0000_0000_1111_1111_1111_1111;
        private const int ID_PASS_BITSHIFT = 16;
        private const int SHIFTED_FACES_MASK = 0b0011_1111;
        private const int FACES_MASK = 0b000_0000_0011_1111_0000_0000_0000_0000;
        private const int FACES_PASS_BITSHIFT = 22;
        private const int TRANSPARENCY_MASK = 0b000_0000_0100_0000_0000_0000_0000_0000;
        private const int TRANSPARENCY_PASS_BITSHIFT = 23;
        private const int DAMAGE_MASK = 0b000_0111_1000_0000_0000_0000_0000_0000;
        private const int DAMAGE_PASS_BITSHIFT = 27;
        private const int LIGHT_LEVEL_MASK = 0b111_1000_0000_0000_0000_0000_0000_0000;

        public int Value;

        public ushort Id
        {
            get => (ushort) (Value & ID_MASK);
            private set => Value = (Value & ~ID_MASK) | (value & ID_MASK);
        }

        public byte Faces
        {
            get => (byte) ((Value & FACES_MASK) >> ID_PASS_BITSHIFT);
            set => Value = (Value & ~FACES_MASK) | ((value << ID_PASS_BITSHIFT) & FACES_MASK);
        }

        public bool Transparent
        {
            get => ((Value & TRANSPARENCY_MASK) >> FACES_PASS_BITSHIFT) == 0;
            // value = true is transparent so that the default value of block is transparent
            private set => SetTransparency(value);
        }

        public byte Damage
        {
            get => (byte) (Value & (DAMAGE_MASK >> TRANSPARENCY_PASS_BITSHIFT));
            set => Value = (Value & ~DAMAGE_MASK) | ((value << FACES_PASS_BITSHIFT) & DAMAGE_MASK);
        }

        public byte LightLevel
        {
            get => (byte) ((Value & LIGHT_LEVEL_MASK) >> DAMAGE_PASS_BITSHIFT);
            set => Value = (Value & ~LIGHT_LEVEL_MASK) | ((value << DAMAGE_PASS_BITSHIFT) & LIGHT_LEVEL_MASK);
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

        public bool HasAnyFaces() => Faces > 0;

        public bool HasAllFaces() => Faces >= SHIFTED_FACES_MASK;

        public bool HasFace(Direction direction) => (Value & ((byte) direction << ID_PASS_BITSHIFT)) > 0;

        public void SetFace(Direction direction, bool boolean)
        {
            Value = Value.SetBitByBoolWithMask((byte) direction << ID_PASS_BITSHIFT, boolean);
        }

        public void SetTransparency(bool transparent)
        {
            Value = Value.SetBitByBoolWithMask(TRANSPARENCY_MASK, !transparent);
        }

        public void ClearFaces()
        {
            Faces = 0;
        }
    }
}
