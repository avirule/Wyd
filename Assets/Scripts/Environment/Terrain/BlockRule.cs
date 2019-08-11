#region

using UnityEngine;

#endregion

namespace Environment.Terrain
{
    public delegate string RuleEvaluation(Vector3Int position, Direction direction);

    public interface IBlockRule
    {
        ushort Id { get; }
        string BlockName { get; }
        bool IsTransparent { get; }
        RuleEvaluation RuleEvaluation { get; }

        bool ReadUVsRule(ushort blockId, Vector3Int position, Direction direction, out string spriteName);
    }

    public struct BlockRule : IBlockRule
    {
        public ushort Id { get; }
        public string BlockName { get; }
        public bool IsTransparent { get; }
        public RuleEvaluation RuleEvaluation { get; }

        public BlockRule(ushort id, string blockName, bool isTransparent, RuleEvaluation ruleEvaluation)
        {
            Id = id;
            BlockName = blockName;
            IsTransparent = isTransparent;
            RuleEvaluation = ruleEvaluation ?? ((position, direction) => string.Empty);
        }

        public bool ReadUVsRule(ushort blockId, Vector3Int position, Direction direction, out string spriteName)
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