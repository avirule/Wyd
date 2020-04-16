#region

using Unity.Mathematics;

#endregion

namespace Wyd.Game.World.Blocks
{
    public interface IBlockDefinition : IReadOnlyBlockDefinition
    {
        bool EvaluateUVsRule(ushort blockId, int3 position, Direction direction, out string spriteName);
    }
}
