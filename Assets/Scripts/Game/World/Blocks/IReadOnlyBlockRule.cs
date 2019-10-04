namespace Game.World.Blocks
{
    public interface IReadOnlyBlockRule
    {
        Block.Types Type { get; }
        string BlockName { get; }
        bool Transparent { get; }
        bool Collideable { get; }
        bool Destroyable { get; }
        bool Collectible { get; }
        bool LightSource { get; }
        byte LightLevel { get; }
    }
}
