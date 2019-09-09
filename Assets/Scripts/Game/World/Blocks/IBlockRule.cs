#region

using UnityEngine;

#endregion

namespace Game.World.Blocks
{
    public interface IBlockRule
    {
        ushort Id { get; }
        string BlockName { get; }
        bool Transparent { get; }

        bool ReadUVsRule(ushort blockId, Vector3 position, Direction direction, out string spriteName);
    }
}
