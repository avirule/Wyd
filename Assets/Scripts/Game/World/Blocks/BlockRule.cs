#region

using System;
using System.Collections.Specialized;
using Serilog;
using UnityEngine;
using Random = System.Random;

// ReSharper disable TypeParameterCanBeVariant

#endregion

namespace Wyd.Game.World.Blocks
{
    public class BlockRule : IBlockRule
    {
        [Flags]
        public enum Property
        {
            Transparent = 1,
            Collideable = 2,
            Destroyable = 4,
            Collectible = 8,
            LightSource = 16
        }

        private static readonly Func<Vector3, Direction, string> _DefaultUVsRule;

        static BlockRule()
        {
            _DefaultUVsRule = (position, direction) => string.Empty;
        }

        private Func<Vector3, Direction, string> UVsRule { get; }

        private BitVector32 _Bits;

        public ushort Id { get; }

        public string BlockName { get; }
        public Property Properties { get; }

        public Block.Types Type { get; }

        public bool Transparent => (Properties & Property.Transparent) > 0;

        public bool Collideable => (Properties & Property.Collideable) > 0;

        public bool Destroyable => (Properties & Property.Destroyable) > 0;

        public bool Collectible => (Properties & Property.Collectible) > 0;

        public bool LightSource => (Properties & Property.LightSource) > 0;
        public byte LightLevel { get; }

        public BlockRule(ushort id, string blockName, Block.Types type,
            Func<Vector3, Direction, string> uvsRule, params Property[] properties)
        {
            _Bits = new BitVector32(0);

            Id = id;
            BlockName = blockName;
            Type = type;

            foreach (Property property in properties)
            {
                Properties |= property;
            }

            UVsRule = uvsRule ?? _DefaultUVsRule;
        }

        public virtual bool ReadUVsRule(ushort blockId, Vector3 position, Direction direction, out string spriteName)
        {
            if (Id != blockId)
            {
                Log.Warning(
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
