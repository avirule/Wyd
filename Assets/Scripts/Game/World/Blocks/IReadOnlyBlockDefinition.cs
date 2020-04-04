namespace Wyd.Game.World.Blocks
{
    public interface IReadOnlyBlockDefinition
    {
        Block.Types Type { get; }
        string BlockName { get; }
        BlockDefinition.Property Properties { get; }
        bool Transparent { get; }
        bool Collideable { get; }
        bool Destroyable { get; }
        bool Collectible { get; }
        bool LightSource { get; }
        byte LightLevel { get; }
    }
}
