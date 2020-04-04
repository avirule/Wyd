#region

using UnityEngine;
using Random = System.Random;

#endregion

namespace Wyd.Game.World.Blocks
{
    public interface IBlockDefinition : IReadOnlyBlockDefinition
    {
        bool ReadUVsRule(ushort blockId, Vector3 position, Direction direction, out string spriteName);
        bool ShouldPlaceAt(Random rand, int index, Vector3 position, Block[] blocks);
    }
}
