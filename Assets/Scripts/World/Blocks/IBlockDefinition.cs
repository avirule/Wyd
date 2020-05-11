namespace Wyd.World.Blocks
{
    public interface IBlockDefinition : IReadOnlyBlockDefinition
    {
        bool GetUVs(Direction direction, out string spriteName);
    }
}
