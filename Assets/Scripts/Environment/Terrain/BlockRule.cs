using UnityEngine;

// ReSharper disable MemberCanBePrivate.Global

namespace Environment.Terrain
{
    public delegate string RuleEvaluation(Vector3Int position, Direction direction);

    public class BlockRule
    {
        protected readonly string BlockName;

        public readonly bool IsTransparent;
        protected RuleEvaluation RuleEvaluation;

        public BlockRule(string blockName, bool isTransparent, RuleEvaluation ruleEvaluation)
        {
            BlockName = blockName;
            IsTransparent = isTransparent;
            RuleEvaluation = ruleEvaluation ?? ((position, direction) => string.Empty);
        }

        public bool SetRuleEvaluation(RuleEvaluation ruleEvaluation)
        {
            RuleEvaluation = ruleEvaluation;
            return true;
        }

        public bool ReadRule(string blockName, Vector3Int position, Direction direction, out string spriteName)
        {
            if (!BlockName.Equals(blockName))
            {
                Debug.Log(
                    $"Failed to get rule of specified block `{blockName}`: block name mismatch (referenced {blockName}, targeted {BlockName}).");
                spriteName = string.Empty;
                return false;
            }

            spriteName = RuleEvaluation(position, direction);
            return true;
        }
    }
}