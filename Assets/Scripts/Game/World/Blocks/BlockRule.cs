#region

using System;
using UnityEngine;
using Random = System.Random;

// ReSharper disable TypeParameterCanBeVariant

#endregion

namespace Game.World.Blocks
{
    public class BlockRule : IBlockRule
    {
        private Func<Vector3, Direction, string> UVsRule { get; }

        public ushort Id { get; }
        public string BlockName { get; }
        public Block.Types Type { get; }
        public bool Transparent { get; }
        public bool Collideable { get; }
        public bool Destroyable { get; }

        public BlockRule(
            ushort id, string blockName, Block.Types type, bool transparent, bool collideable, bool destroyable, Func<Vector3, Direction, string> uvsRule)
        {
            UVsRule = uvsRule ?? ((position, direction) => string.Empty);

            Id = id;
            BlockName = blockName;
            Type = type;
            Transparent = transparent;
            Collideable = collideable;
            Destroyable = destroyable;
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

        public virtual bool ShouldPlaceAt(Random rand, int index, Vector3 position, Block[] blocks)
        {
            // todo implement placement choicing
            return false;
        }
    }
}
