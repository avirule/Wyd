using System.Collections.Generic;
using Entities;
using Environment.Terrain;
using Logging;
using NLog;
using UnityEngine;

namespace Controllers
{
    public class BlockController : MonoBehaviour
    {
        private Dictionary<string, BlockRule> _Blocks;
        public TextureController TextureController;

        public Dictionary<string, BlockRule>.KeyCollection RegisteredBlocks => _Blocks.Keys;

        public void Awake()
        {
            _Blocks = new Dictionary<string, BlockRule>();
        }

        public bool RegisterBlockRules(string blockName, bool addNewBlock, bool isTransparent,
            RuleEvaluation ruleEvaluation)
        {
            if (!_Blocks.ContainsKey(blockName))
            {
                if (!addNewBlock)
                {
                    EventLog.Logger.Log(LogLevel.Error, $"Failed to add block rule: specified block `{blockName}` does not exist.");

                    return false;
                }

                EventLog.Logger.Log(LogLevel.Warn, $"AddNewBlock flag set, adding block `{blockName}` and continuing...");

                _Blocks.Add(blockName, new BlockRule(blockName, isTransparent, null));
            }

            _Blocks[blockName].SetRuleEvaluation(ruleEvaluation);

            EventLog.Logger.Log(LogLevel.Info, $"Successfully added rule evaluation for block `{blockName}`.");

            return true;
        }

        public bool GetBlockSpriteUVs(string blockName, Vector3Int position, Direction direction, out Vector2[] uvs)
        {
            uvs = null;

            if (!_Blocks.ContainsKey(blockName))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block sprite UVs for direction `{direction}` of block `{blockName}`: block does not exist.");
                return false;
            }

            _Blocks[blockName].ReadRule(blockName, position, direction, out string spriteName);

            if (!TextureController.Sprites.ContainsKey(spriteName))
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to return block sprite UVs for direction `{direction}` of block `{blockName}`: sprite does not exist for block.");
                return false;
            }

            uvs = TextureController.Sprites[spriteName];
            return true;
        }

        public bool IsBlockTransparent(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
            {
                return true;
            }

            if (!_Blocks.ContainsKey(blockName))
            {
                EventLog.Logger.Log(LogLevel.Error, $"Failed to return block rule for block `{blockName}`: block does not exist.");
                return false;
            }

            return _Blocks[blockName].IsTransparent;
        }
    }
}