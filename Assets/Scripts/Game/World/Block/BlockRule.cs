#region

using UnityEngine;

// ReSharper disable TypeParameterCanBeVariant

#endregion

namespace Game.World
{
    public delegate string RuleEvaluation<T1>(T1 arg1);

    public delegate string RuleEvaluation<T1, T2>(T1 arg1, T2 arg2);

    public delegate string RuleEvaluation<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3);

    public delegate string RuleEvaluation<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);

    public delegate string RuleEvaluation<T1, T2, T3, T4, T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

    public delegate string RuleEvaluation<T1, T2, T3, T4, T5, T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);

    public struct BlockRule : IBlockRule
    {
        private RuleEvaluation<Vector3, Direction> UVsRule { get; }

        public ushort Id { get; }
        public string BlockName { get; }
        public bool Transparent { get; }

        public BlockRule(ushort id, string blockName, bool transparent, RuleEvaluation<Vector3, Direction> uvsRule)
        {
            UVsRule = uvsRule ?? ((position, direction) => string.Empty);

            Id = id;
            BlockName = blockName;
            Transparent = transparent;
        }

        public bool ReadUVsRule(ushort blockId, Vector3 position, Direction direction, out string spriteName)
        {
            if (Id != blockId)
            {
                Debug.Log(
                    $"Failed to get rule of specified block `{blockId}`: block name mismatch (referenced {blockId}, targeted {BlockName}).");
                spriteName = string.Empty;
                return false;
            }

            spriteName = UVsRule(position, direction);
            return true;
        }
    }
}