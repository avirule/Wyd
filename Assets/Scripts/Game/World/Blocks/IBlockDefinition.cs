#region

using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Wyd.Game.World.Blocks
{
    public interface IBlockDefinition : IReadOnlyBlockDefinition
    {
        bool EvaluateUVsRule(ushort blockId, int3 position, Direction direction, out string spriteName);
        bool ShouldPlaceAt(Random rand, int index, Vector3 position, Block[] blocks);
    }
}
