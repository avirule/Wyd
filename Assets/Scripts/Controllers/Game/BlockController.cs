#region

using System;
using System.Collections.Generic;
using System.Linq;
using Environment;
using Environment.Terrain;
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

        public Dictionary<ushort, IBlockRule>.KeyCollection RegisteredBlocks => _Blocks.Keys;

        private void Awake()
        {
            if (Current != null && Current != this)
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
            ushort blockId = 0;

            try
            {
                blockId = RegisteredBlocks.Count == 0 ? (ushort) 1 : Convert.ToUInt16(RegisteredBlocks.Max() + 1);
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

        public bool GetBlockSpriteUVs(ushort blockId, Vector3Int position, Direction direction, out Vector2[] uvs)
        {
            uvs = null;

            if (!_Blocks.ContainsKey(blockId))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: block id does not exist.");
                return false;
            }

            _Blocks[blockId].ReadUVsRule(blockId, position, direction, out string spriteName);

            if (!TextureController.Sprites.ContainsKey(spriteName))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block sprite UVs for direction `{direction}` of block with id `{blockId}`: sprite does not exist for block.");
                return false;
            }

            uvs = TextureController.Sprites[spriteName];
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
            if (!_BlockNameIds.ContainsKey(blockName))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to return block id for block `{blockName}`: block does not exist.");
            }

            return _BlockNameIds[blockName];
        }
    }
}