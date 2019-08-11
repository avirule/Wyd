#region

using System.Collections.Generic;
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

        private Dictionary<ushort, IBlockRule> _Blocks;
        public TextureController TextureController;

        public Dictionary<ushort, IBlockRule>.KeyCollection RegisteredBlocks => _Blocks.Keys;

        private void Awake()
        {
            _Blocks = new Dictionary<ushort, IBlockRule>();
            Block.AssignBlockController(this);
        }

        public bool RegisterBlockRules(ushort blockId, string blockName, bool addNewBlock, bool isTransparent,
            RuleEvaluation ruleEvaluation)
        {
            if (blockId == 0)
            {
                EventLog.Logger.Log(LogLevel.Error, "Failed to add block rule: cannot register block with id of 0.");
            }

            if (!_Blocks.ContainsKey(blockId))
            {
                if (!addNewBlock)
                {
                    EventLog.Logger.Log(LogLevel.Error,
                        $"Failed to add block rule: specified block id `{blockId}` does not exist.");

                    return false;
                }

                EventLog.Logger.Log(LogLevel.Warn,
                    $"AddNewBlock flag set, adding block `{blockName}` with id `{blockId}` and continuing...");

                _Blocks.Add(blockId, new BlockRule(blockId, blockName, isTransparent, ruleEvaluation));
            }

            EventLog.Logger.Log(LogLevel.Info,
                $"Successfully added rule evaluation for block `{blockName}` with id `{blockId}`.");

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

            return _Blocks[blockId].IsTransparent;
        }
    }
}