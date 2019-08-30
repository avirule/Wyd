#region

using UnityEngine;

#endregion

namespace Game.World
{
    public interface IBlockRule
    {
        ushort Id { get; }
        string BlockName { get; }
        bool Transparent { get; }

        bool ReadUVsRule(ushort blockId, Vector3Int position, Direction direction, out string spriteName);
    }
}