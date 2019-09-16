#region

using UnityEngine;

#endregion

namespace Game.World.Blocks
{
    public interface IBlockRule
    {
        ushort Id { get; }
        Block.Types Type { get; }
        string BlockName { get; }
        bool Transparent { get; }

        bool ReadUVsRule(ushort blockId, Vector3 position, Direction direction, out string spriteName);
        bool ShouldPlaceAt(System.Random rand, int index, Vector3 position, Block[] blocks);
    }
}
