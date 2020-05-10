namespace Wyd.Game.World.Blocks
{
    public interface IBlockDefinition : IReadOnlyBlockDefinition
    {
        bool EvaluateUVsRule(ushort blockId, Direction direction, out string spriteName);
    }
}
