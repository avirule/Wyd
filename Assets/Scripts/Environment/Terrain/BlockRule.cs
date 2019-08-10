using UnityEngine;

// ReSharper disable MemberCanBePrivate.Global

namespace Environment.Terrain
{
    public delegate string RuleEvaluation(Vector3Int position, Direction direction);

    public class BlockRule
    {
        protected readonly string BlockName;
        protected readonly ushort Id;

        public readonly bool IsTransparent;
        protected RuleEvaluation RuleEvaluation;

        public BlockRule(ushort id, string blockName, bool isTransparent, RuleEvaluation ruleEvaluation)
        {
            Id = id;
            BlockName = blockName;
            IsTransparent = isTransparent;
            RuleEvaluation = ruleEvaluation ?? ((position, direction) => string.Empty);
        }

        public bool SetRuleEvaluation(RuleEvaluation ruleEvaluation)
        {
            RuleEvaluation = ruleEvaluation;
            return true;
        }

        public bool ReadRule(ushort blockId, Vector3Int position, Direction direction, out string spriteName)
        {
            if (Id != blockId)
            {
                Debug.Log(
                    $"Failed to get rule of specified block `{blockId}`: block name mismatch (referenced {blockId}, targeted {BlockName}).");
                spriteName = string.Empty;
                return false;
            }

            spriteName = RuleEvaluation(position, direction);
            return true;
        }
    }
}