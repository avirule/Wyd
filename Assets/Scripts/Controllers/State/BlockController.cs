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

        public static Block Air = new Block(0);

        public Dictionary<string, ushort> BlockNameIds;
        public Dictionary<ushort, IBlockRule> Blocks;

        private void Awake()
        {
            AssignCurrent(this);
            BlockNameIds = new Dictionary<string, ushort>();
            Blocks = new Dictionary<ushort, IBlockRule>();
        }

        public ushort RegisterBlockRules(
            string blockName, Block.Types type, bool transparent, bool collideable, bool destroyable,
            Func<Vector3, Direction, string> uvsRule = default)
        {
            ushort blockId = 0;

            try
            {
                blockId = Blocks.Count == 0 ? (ushort) 1 : Convert.ToUInt16(Blocks.Max(kvp => kvp.Key) + 1);
            }
            catch (OverflowException)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    "BlockController has registered too many blocks and is out of valid block ids.");
            }

            if (uvsRule == default)
            {
                uvsRule = (position, direction) => blockName;
            }

            if (!Blocks.ContainsKey(blockId))
            {
                Blocks.Add(blockId,
                    new BlockRule(blockId, blockName, type, transparent, collideable, destroyable, uvsRule));
                BlockNameIds.Add(blockName, blockId);
            }

            EventLog.Logger.Log(LogLevel.Info,
                $"Successfully added block `{blockName}` with ID: {blockId}");

            return blockId;
        }

        public bool GetBlockSpriteUVs(
            ushort blockId, Vector3 position, Direction direction, Vector3 size2d,
            out Vector3[] uvs)
        {
            uvs = null;

            if (!Blocks.ContainsKey(blockId))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: block id does not exist.");
                return false;
            }

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

        public bool BlockIdExists(ushort blockId)
        {
            return Blocks.ContainsKey(blockId);
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
            if (blockId == BLOCK_EMPTY_ID)
            {
                return "air";
            }

            if (!Blocks.TryGetValue(blockId, out IBlockRule blockRule))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to return block name for block id `{blockId}`: block does not exist.");
                return "null";
            }

            return blockRule.BlockName;
        }

        public bool IsBlockDefaultTransparent(ushort blockId)
        {
            if (blockId == BLOCK_EMPTY_ID)
            {
                return true;
            }

            if (!Blocks.TryGetValue(blockId, out IBlockRule blockRule))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block rule for block with id `{blockId}`: block does not exist.");
                return false;
            }

            return blockRule.Transparent;
        }

        public bool IsBlockDefaultCollideable(ushort blockId)
        {
            if (blockId == BLOCK_EMPTY_ID)
            {
                return false;
            }

            if (!Blocks.TryGetValue(blockId, out IBlockRule blockRule))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block rule for block with id `{blockId}`: block does not exist.");
                return false;
            }

            return blockRule.Collideable;
        }

        public bool IsBlockDefaultDestroyable(ushort blockId)
        {
            if (blockId == BLOCK_EMPTY_ID)
            {
                return false;
            }

            if (!Blocks.TryGetValue(blockId, out IBlockRule blockRule))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block rule for block with id `{blockId}`: block does not exist.");
                return false;
            }

            return blockRule.Destroyable;
        }

        public IEnumerable<IBlockRule> GetBlocksOfType(Block.Types type)
        {
            return Blocks.Values.Where(block => block.Type == type);
        }
    }
}
