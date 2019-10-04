#region

using System;
using System.Collections.Specialized;
using UnityEngine;
using Random = System.Random;

// ReSharper disable TypeParameterCanBeVariant

#endregion

namespace Game.World.Blocks
{
    public class BlockRule : IBlockRule
    {
        private static readonly BitVector32.Section IdSection;
        private static readonly BitVector32.Section TypeSection;
        private static readonly BitVector32.Section TransparencySection;
        private static readonly BitVector32.Section CollideableSection;
        private static readonly BitVector32.Section DestroyableSection;
        private static readonly BitVector32.Section CollectibleSection;
        private static readonly BitVector32.Section LightSourceSection;
        private static readonly BitVector32.Section LightLevelSection;

        private static readonly Func<Vector3, Direction, string> DefaultUVsRule;

        static BlockRule()
        {
            IdSection = BitVector32.CreateSection(short.MaxValue);
            TypeSection = BitVector32.CreateSection(15, IdSection); // maximum of 4 bits type accuracy
            TransparencySection = BitVector32.CreateSection(1, TypeSection);
            CollideableSection = BitVector32.CreateSection(1, TransparencySection);
            DestroyableSection = BitVector32.CreateSection(1, CollideableSection);
            CollectibleSection = BitVector32.CreateSection(1, DestroyableSection);
            LightSourceSection = BitVector32.CreateSection(1, CollectibleSection);
            LightLevelSection = BitVector32.CreateSection(15, LightSourceSection);

            DefaultUVsRule = (position, direction) => string.Empty;
        }

        private Func<Vector3, Direction, string> UVsRule { get; }

        private BitVector32 _Bits;

        public ushort Id
        {
            get => (ushort) _Bits[IdSection];
            private set => _Bits[IdSection] = (short) value;
        }

        public string BlockName { get; }

        public Block.Types Type
        {
            get => (Block.Types) _Bits[TypeSection];
            private set => _Bits[TypeSection] = (short) value;
        }

        public bool Transparent
        {
            get => _Bits[TransparencySection] == 1;
            private set => _Bits[TransparencySection] = value ? 1 : 0;
        }

        public bool Collideable
        {
            get => _Bits[CollideableSection] == 1;
            private set => _Bits[CollideableSection] = value ? 1 : 0;
        }

        public bool Destroyable
        {
            get => _Bits[DestroyableSection] == 1;
            private set => _Bits[DestroyableSection] = value ? 1 : 0;
        }

        public bool Collectible
        {
            get => _Bits[CollectibleSection] == 1;
            private set => _Bits[CollectibleSection] = value ? 1 : 0;
        }

        public bool LightSource { get; }
        public byte LightLevel { get; }

        public BlockRule(
            ushort id, string blockName, Block.Types type,
            bool transparent, bool collideable, bool destroyable, bool collectible,
            Func<Vector3, Direction, string> uvsRule)
        {
            _Bits = new BitVector32(0);

            Id = id;
            BlockName = blockName;
            Type = type;
            Transparent = transparent;
            Collideable = collideable;
            Destroyable = destroyable;
            Collectible = collectible;

            UVsRule = uvsRule ?? DefaultUVsRule;
        }

        public virtual bool ReadUVsRule(ushort blockId, Vector3 position, Direction direction, out string spriteName)
        {
            if (Id != blockId)
            {
                Debug.Log(
                    $"Failed to get rule of specified block `{blockId}`: block name mismatch (referenced {blockId}, targeted {BlockName}).");
                spriteName = string.Empty;
                return false;
            }

            spriteName = UVsRule(position, direction);
            return true;
        }

        public virtual bool ShouldPlaceAt(Random rand, int index, Vector3 position, Block[] blocks) => false;
    }
}
