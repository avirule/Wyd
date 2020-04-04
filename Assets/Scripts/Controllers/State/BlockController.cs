#region

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using UnityEngine;
using Wyd.Game;
using Wyd.Game.World.Blocks;
using Wyd.Graphics;

#endregion

namespace Wyd.Controllers.State
{
    public class BlockController : SingletonController<BlockController>
    {
        public const ushort AIR_ID = 0;
        public Dictionary<string, ushort> BlockNames;
        public List<IBlockDefinition> BlockDefinitions;

        private void Awake()
        {
            AssignSingletonInstance(this);

            BlockNames = new Dictionary<string, ushort>();
            BlockDefinitions = new List<IBlockDefinition>();

            RegisterBlockDefinition("Air", Block.Types.None, null, BlockDefinition.Property.Transparent);
        }

        public ushort RegisterBlockDefinition(string blockName, Block.Types type,
            Func<Vector3, Direction, string> uvsRule, params BlockDefinition.Property[] properties)
        {
            ushort assignedBlockId;

            try
            {
                assignedBlockId = (ushort)BlockDefinitions.Count;
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

            BlockDefinitions.Add(new BlockDefinition(assignedBlockId, blockName, type, uvsRule, properties));
            BlockNames.Add(blockName, assignedBlockId);

            Log.Information($"Successfully added block `{blockName}` with ID: {assignedBlockId}");

            return assignedBlockId;
        }

        public bool GetBlockSpriteUVs(ushort blockId, Vector3 position, Direction direction, Vector3 size2d,
            out BlockUVs blockUVs)
        {
            if (!BlockIdExists(blockId))
            {
                Log.Error(
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: block id does not exist.");
                blockUVs = null;
                return false;
            }

            BlockDefinitions[blockId].ReadUVsRule(blockId, position, direction, out string textureName);
            if (!TextureController.Current.TryGetTextureId(textureName, out int textureId))
            {
                Log.Warning(
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: texture does not exist for block.");
                blockUVs = null;
                return false;
            }

            blockUVs = new BlockUVs(new Vector3(0, 0, textureId), new Vector3(size2d.x, 0, textureId),
                new Vector3(0, size2d.z, textureId), new Vector3(size2d.x, size2d.z, textureId));

            Log.Verbose($"Block `{textureName}:{blockId}` returned block UVs `{blockUVs}`.");

            return true;
        }

        public bool BlockIdExists(ushort blockId) => blockId < BlockDefinitions.Count;

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
                return BlockDefinitions[blockId].BlockName;
            }

            Log.Warning($"Failed to return block name for block id `{blockId}`: block does not exist.");
            return "null";
        }

        public IReadOnlyBlockDefinition GetBlockDefinition(ushort blockId)
        {
            if (BlockIdExists(blockId))
            {
                return BlockDefinitions[blockId];
            }

            Log.Error($"Failed to return block rule for block with id `{blockId}`: block does not exist.");
            return null;
        }

        public bool TryGetBlockRule(ushort blockId, out IReadOnlyBlockDefinition blockDefinition)
        {
            if (BlockIdExists(blockId))
            {
                blockDefinition = BlockDefinitions[blockId];
                return true;
            }

            Log.Error($"Failed to return block rule for block with id `{blockId}`: block does not exist.");

            blockDefinition = default;
            return false;
        }

        public IEnumerable<IBlockDefinition> GetBlockDefinitionsByType(Block.Types type)
        {
            return BlockDefinitions.Where(block => block.Type == type);
        }

        public bool CheckBlockHasProperties(ushort blockId, BlockDefinition.Property property)
        {
            if (blockId < BlockDefinitions.Count)
            {
                return (BlockDefinitions[blockId].Properties & property) > 0;
            }
            else
            {
                return false;
            }
        }
    }
}
