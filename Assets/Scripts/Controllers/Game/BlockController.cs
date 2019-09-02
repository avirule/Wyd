#region

using System;
using System.Collections.Concurrent;
using System.Linq;
using Game;
using Game.World.Block;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.Game
{
    public class BlockController : SingletonController<BlockController>
    {
        public const ushort BLOCK_EMPTY_ID = 0;

        public ConcurrentDictionary<string, ushort> BlockNameIds;
        public ConcurrentDictionary<ushort, IBlockRule> Blocks;

        private void Awake()
        {
            if ((Current != null) && (Current != this))
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }

            BlockNameIds = new ConcurrentDictionary<string, ushort>();
            Blocks = new ConcurrentDictionary<ushort, IBlockRule>();
        }

        public int RegisterBlockRules(string blockName, bool isTransparent,
            RuleEvaluation<Vector3, Direction> uvsRule = default)
        {
            blockName = blockName.ToLowerInvariant();
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
                Blocks.TryAdd(blockId, new BlockRule(blockId, blockName, isTransparent, uvsRule));
                BlockNameIds.TryAdd(blockName, blockId);
            }

            EventLog.Logger.Log(LogLevel.Info,
                $"Successfully added block `{blockName}` with id `{blockId}`.");

            return blockId;
        }

        public bool GetBlockSpriteUVs(ushort blockId, Vector3 position, Direction direction, Vector3 size2d,
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

        public bool IsBlockDefaultTransparent(ushort blockId)
        {
            if (blockId == 0)
            {
                return true;
            }

            if (!Blocks.ContainsKey(blockId))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block rule for block with id `{blockId}`: block does not exist.");
                return false;
            }

            return Blocks[blockId].Transparent;
        }

        public ushort GetBlockId(string blockName)
        {
            blockName = blockName.ToLowerInvariant();

            if (!BlockNameIds.ContainsKey(blockName))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to return block id for block `{blockName}`: block does not exist.");
                return 0;
            }

            return BlockNameIds[blockName];
        }

        public string GetBlockName(ushort blockId)
        {
            if (blockId == BLOCK_EMPTY_ID)
            {
                return "Air";
            }

            if (!Blocks.ContainsKey(blockId))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to return block name for block id `{blockId}`: block does not exist.");
                return "Null";
            }

            return Blocks[blockId].BlockName;
        }

        public bool IsBlockTransparent(ushort id)
        {
            return !Blocks.ContainsKey(id) || Blocks[id].Transparent;
        }
    }
}