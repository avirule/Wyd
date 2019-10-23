#region

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using UnityEngine;
using Wyd.Game;
using Wyd.Game.World.Blocks;

#endregion

namespace Wyd.Controllers.State
{
    public class BlockController : SingletonController<BlockController>
    {
        public const ushort AIR_ID = 0;

        private Dictionary<BlockRule.Property, HashSet<ushort>> _BlockPropertiesRegistry;

        public Dictionary<string, ushort> BlockNames;
        public List<IBlockRule> BlockRules;

        private void Awake()
        {
            AssignCurrent(this);

            _BlockPropertiesRegistry = new Dictionary<BlockRule.Property, HashSet<ushort>>();

            // add registry entry for each property type
            foreach (BlockRule.Property property in Enum.GetValues(typeof(BlockRule.Property)))
            {
                _BlockPropertiesRegistry.Add(property, new HashSet<ushort>());
            }

            BlockNames = new Dictionary<string, ushort>();
            BlockRules = new List<IBlockRule>
            {
                // add first value so count aligns with ids
                null
            };
        }

        public ushort RegisterBlockRule(string blockName, Block.Types type,
            Func<Vector3, Direction, string> uvsRule, params BlockRule.Property[] properties)
        {
            ushort assignedBlockId;

            try
            {
                assignedBlockId = (ushort)BlockRules.Count;
            }
            catch (OverflowException)
            {
                Log.Error("BlockController has registered too many blocks and is out of valid block ids.");
                return ushort.MaxValue;
            }

            if (uvsRule == default)
            {
                uvsRule = (position, direction) => blockName;
            }

            BlockRules.Add(new BlockRule(assignedBlockId, blockName, type, uvsRule, properties));
            BlockNames.Add(blockName, assignedBlockId);

            foreach (BlockRule.Property property in properties)
            {
                _BlockPropertiesRegistry[property].Add(assignedBlockId);
            }

            Log.Information($"Successfully added block `{blockName}` with ID: {assignedBlockId}");

            return assignedBlockId;
        }

        public bool GetBlockSpriteUVs(ushort blockId, Vector3 position, Direction direction, Vector3 size2d,
            out Vector3[] uvs)
        {
            if (!BlockIdExists(blockId))
            {
                Log.Error(
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: block id does not exist.");
                uvs = null;
                return false;
            }

            BlockRules[blockId].ReadUVsRule(blockId, position, direction, out string textureName);

            if (!TextureController.Current.TryGetTextureId(textureName, out int textureId))
            {
                Log.Warning(
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: texture does not exist for block.");
                uvs = null;
                return false;
            }

            uvs = new[]
            {
                new Vector3(0, 0, textureId),
                new Vector3(size2d.x, 0, textureId),
                new Vector3(0, size2d.z, textureId),
                new Vector3(size2d.x, size2d.z, textureId)
            };

            return true;
        }

        public bool BlockIdExists(ushort blockId) => blockId < BlockRules.Count;

        public ushort GetBlockId(string blockName)
        {
            if (!BlockNames.TryGetValue(blockName, out ushort blockId))
            {
                Log.Warning($"Failed to return block id for block `{blockName}`: block does not exist.");
                return AIR_ID;
            }

            return blockId;
        }

        public bool TryGetBlockId(string blockName, out ushort blockId)
        {
            if (!BlockNames.TryGetValue(blockName, out blockId))
            {
                blockId = 0;
                return false;
            }

            return true;
        }

        public string GetBlockName(ushort blockId)
        {
            if (BlockIdExists(blockId))
            {
                return BlockRules[blockId].BlockName;
            }

            Log.Warning($"Failed to return block name for block id `{blockId}`: block does not exist.");
            return "null";
        }

        public IReadOnlyBlockRule GetBlockRule(ushort blockId)
        {
            if (BlockIdExists(blockId))
            {
                return BlockRules[blockId];
            }

            Log.Error($"Failed to return block rule for block with id `{blockId}`: block does not exist.");
            return null;
        }

        public bool TryGetBlockRule(ushort blockId, out IReadOnlyBlockRule blockRule)
        {
            if (BlockIdExists(blockId))
            {
                blockRule = BlockRules[blockId];
                return true;
            }

            Log.Error($"Failed to return block rule for block with id `{blockId}`: block does not exist.");

            blockRule = default;
            return false;
        }

        public IEnumerable<IBlockRule> GetBlocksOfType(Block.Types type)
        {
            return BlockRules.Where(block => block.Type == type);
        }

        public bool CheckBlockHasProperty(ushort blockId, BlockRule.Property property) =>
            _BlockPropertiesRegistry[property].Contains(blockId);
    }
}
