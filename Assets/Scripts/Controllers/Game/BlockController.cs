#region

using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Terrain;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.Game
{
    public class BlockController : MonoBehaviour
    {
        public const ushort BLOCK_EMPTY_ID = 0;

        public static BlockController Current;

        private Dictionary<string, ushort> _BlockNameIds;
        private Dictionary<ushort, IBlockRule> _Blocks;
        public TextureController TextureController;

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

            _BlockNameIds = new Dictionary<string, ushort>();
            _Blocks = new Dictionary<ushort, IBlockRule>();
        }

        public bool RegisterBlockRules(string blockName, bool isTransparent, RuleEvaluation uvsRule = default)
        {
            blockName = blockName.ToLowerInvariant();
            ushort blockId = 0;

            try
            {
                blockId = _Blocks.Count == 0 ? (ushort) 1 : Convert.ToUInt16(_Blocks.Max(kvp => kvp.Key) + 1);
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

            if (!_Blocks.ContainsKey(blockId))
            {
                _Blocks.Add(blockId, new BlockRule(blockId, blockName, isTransparent, uvsRule));
                _BlockNameIds.Add(blockName, blockId);
            }

            EventLog.Logger.Log(LogLevel.Info,
                $"Successfully added block `{blockName}` with id `{blockId}`.");

            return true;
        }

        public bool GetBlockSpriteUVs(ushort blockId, Vector3Int position, Direction direction, Vector3 size2d, out Vector3[] uvs)
        {
            uvs = null;

            if (!_Blocks.ContainsKey(blockId))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: block id does not exist.");
                return false;
            }

            _Blocks[blockId].ReadUVsRule(blockId, position, direction, out string textureName);

            if (!TextureController.TryGetTextureId(textureName, out int textureId))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: texture does not exist for block.");
                return false;
            }

            uvs = new[]
            {
                new Vector3(0, 0, textureId),
                new Vector3(size2d.x, 0, textureId),
                new Vector3(0, size2d.y, textureId),
                new Vector3(0, 0, textureId)
            };

            return true;
        }

        public bool IsBlockDefaultTransparent(ushort blockId)
        {
            if (blockId == 0)
            {
                return true;
            }

            if (!_Blocks.ContainsKey(blockId))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block rule for block with id `{blockId}`: block does not exist.");
                return false;
            }

            return _Blocks[blockId].Transparent;
        }

        public ushort GetBlockId(string blockName)
        {
            blockName = blockName.ToLowerInvariant();

            if (!_BlockNameIds.ContainsKey(blockName))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to return block id for block `{blockName}`: block does not exist.");
                return 0;
            }

            return _BlockNameIds[blockName];
        }

        public string GetBlockName(ushort blockId)
        {
            if (blockId == BLOCK_EMPTY_ID)
            {
                return "Air";
            }

            if (!_Blocks.ContainsKey(blockId))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to return block name for block id `{blockId}`: block does not exist.");
                return "Null";
            }

            return _Blocks[blockId].BlockName;
        }

        public bool IsBlockTransparent(ushort id)
        {
            return !_Blocks.ContainsKey(id) || _Blocks[id].Transparent;
        }
    }
}