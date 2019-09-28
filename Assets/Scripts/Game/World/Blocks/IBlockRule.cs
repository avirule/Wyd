#region

using UnityEngine;
using Random = System.Random;

#endregion

namespace Game.World.Blocks
{
    public interface IBlockRule : IReadOnlyBlockRule
    {
        bool ReadUVsRule(ushort blockId, Vector3 position, Direction direction, out string spriteName);
        bool ShouldPlaceAt(Random rand, int index, Vector3 position, Block[] blocks);
    }
}
