namespace Wyd.World.Blocks
{
    public interface IBlockDefinition : IReadOnlyBlockDefinition
    {
        bool EvaluateUVsRule(ushort blockId, Direction direction, out string spriteName);
    }
}
