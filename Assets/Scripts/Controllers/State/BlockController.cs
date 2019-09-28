#region

using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.World.Blocks;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.State
{
    public class BlockController : SingletonController<BlockController>
    {
        public const ushort BLOCK_EMPTY_ID = 0;

        public static Block Air;

        public Dictionary<string, ushort> BlockNameIds;
        public List<IBlockRule> Blocks;

        private void Awake()
        {
            AssignCurrent(this);
            BlockNameIds = new Dictionary<string, ushort>(byte.MaxValue);
            Blocks = new List<IBlockRule>(byte.MaxValue);

            // default 'nothing' block
            RegisterBlockRules("air", Block.Types.None, true, false, false, false);
            Air = new Block(BLOCK_EMPTY_ID);
        }

        public int RegisterBlockRules(
            string blockName, Block.Types type,
            bool transparent, bool collideable, bool destroyable, bool collectible,
            Func<Vector3, Direction, string> uvsRule = default)
        {
            ushort assignedBlockId = 0;

            try
            {
                assignedBlockId = (ushort) Blocks.Count;
            }
            catch (OverflowException)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    "BlockController has registered too many blocks and is out of valid block ids.");
                return -1;
            }

            if (uvsRule == default)
            {
                uvsRule = (position, direction) => blockName;
            }

            Blocks.Add(new BlockRule(assignedBlockId, blockName, type, transparent, collideable, destroyable,
                collectible, uvsRule));
            BlockNameIds.Add(blockName, assignedBlockId);

            EventLog.Logger.Log(LogLevel.Info,
                $"Successfully added block `{blockName}` with ID: {assignedBlockId}");

            return assignedBlockId;
        }

        public bool GetBlockSpriteUVs(
            ushort blockId, Vector3 position, Direction direction, Vector3 size2d,
            out Vector3[] uvs)
        {
            uvs = null;

            if (BlockIdExists(blockId))
            {
                Blocks[blockId].ReadUVsRule(blockId, position, direction, out string textureName);

                if (!TextureController.Current.TryGetTextureId(textureName, out int textureId))
                {
                    EventLog.Logger.Log(LogLevel.Error,
                        $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: texture does not exist for block.");
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

            EventLog.Logger.Log(LogLevel.Error,
                $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: block id does not exist.");
            return false;
        }

        public bool BlockIdExists(ushort blockId)
        {
            return blockId < Blocks.Count;
        }

        public ushort GetBlockId(string blockName)
        {
            if (!BlockNameIds.TryGetValue(blockName, out ushort blockId))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to return block id for block `{blockName}`: block does not exist.");
                return BLOCK_EMPTY_ID;
            }

            return blockId;
        }

        public bool TryGetBlockId(string blockName, out ushort blockId)
        {
            if (!BlockNameIds.TryGetValue(blockName, out blockId))
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
                return Blocks[blockId].BlockName;
            }

            EventLog.Logger.Log(LogLevel.Warn,
                $"Failed to return block name for block id `{blockId}`: block does not exist.");
            return "null";
        }

        public IReadOnlyBlockRule GetBlockRule(ushort blockId)
        {
            if (BlockIdExists(blockId))
            {
                return Blocks[blockId];
            }

            EventLog.Logger.Log(LogLevel.Error,
                $"Failed to return block rule for block with id `{blockId}`: block does not exist.");
            return null;
        }

        public bool TryGetBlockRule(ushort blockId, out IReadOnlyBlockRule blockRule)
        {
            if (BlockIdExists(blockId))
            {
                blockRule = Blocks[blockId];
                return true;
            }

            EventLog.Logger.Log(LogLevel.Error,
                $"Failed to return block rule for block with id `{blockId}`: block does not exist.");

            blockRule = default;
            return false;
        }

        public IEnumerable<IBlockRule> GetBlocksOfType(Block.Types type)
        {
            return Blocks.Where(block => block.Type == type);
        }
    }
}
