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

        private const int _ID_MASK = 0b000_0000_0000_0000_1111_1111_1111_1111;
        private const int _ID_PASS_BITSHIFT = 16;
        private const int _SHIFTED_FACES_MASK = 0b0011_1111;
        private const int _FACES_MASK = 0b000_0000_0011_1111_0000_0000_0000_0000;
        private const int _FACES_PASS_BITSHIFT = 22;
        private const int _TRANSPARENCY_MASK = 0b000_0000_0100_0000_0000_0000_0000_0000;
        private const int _TRANSPARENCY_PASS_BITSHIFT = 23;
        private const int _DAMAGE_MASK = 0b000_0111_1000_0000_0000_0000_0000_0000;
        private const int _DAMAGE_PASS_BITSHIFT = 27;
        private const int _LIGHT_LEVEL_MASK = 0b111_1000_0000_0000_0000_0000_0000_0000;

        public int Value;

        public ushort Id
        {
            get => (ushort)(Value & _ID_MASK);
            private set => Value = (Value & ~_ID_MASK) | (value & _ID_MASK);
        }

        public byte Faces
        {
            get => (byte)((Value & _FACES_MASK) >> _ID_PASS_BITSHIFT);
            set => Value = (Value & ~_FACES_MASK) | ((value << _ID_PASS_BITSHIFT) & _FACES_MASK);
        }

        public bool Transparent
        {
            get => ((Value & _TRANSPARENCY_MASK) >> _FACES_PASS_BITSHIFT) == 0;
            // value = true is transparent so that the default value of block is transparent
            private set => SetTransparency(value);
        }

        public byte Damage
        {
            get => (byte)(Value & (_DAMAGE_MASK >> _TRANSPARENCY_PASS_BITSHIFT));
            set => Value = (Value & ~_DAMAGE_MASK) | ((value << _FACES_PASS_BITSHIFT) & _DAMAGE_MASK);
        }

        public byte LightLevel
        {
            get => (byte)((Value & _LIGHT_LEVEL_MASK) >> _DAMAGE_PASS_BITSHIFT);
            set => Value = (Value & ~_LIGHT_LEVEL_MASK) | ((value << _DAMAGE_PASS_BITSHIFT) & _LIGHT_LEVEL_MASK);
        }

        public void Initialise(ushort id, byte faces = 0, byte damage = 0)
        {
            Id = id;
            Faces = faces;
            Damage = damage;

            if (BlockController.Current.TryGetBlockDefinition(id, out IReadOnlyBlockDefinition blockDefinition))
            {
                Transparent = blockDefinition.Transparent;
                LightLevel = blockDefinition.LightLevel;
            }
        }

        public bool HasAnyFaces() => Faces > 0;

        public bool HasAllFaces() => Faces >= _SHIFTED_FACES_MASK;

        public bool HasFace(Direction direction) => (Value & ((byte)direction << _ID_PASS_BITSHIFT)) > 0;

        public void SetFace(Direction direction, bool boolean)
        {
            Value = Value.SetBitByBoolWithMask((byte)direction << _ID_PASS_BITSHIFT, boolean);
        }

        public void SetTransparency(bool transparent)
        {
            Value = Value.SetBitByBoolWithMask(_TRANSPARENCY_MASK, !transparent);
        }

        public void ClearFaces()
        {
            Faces = 0;
        }
    }
}
