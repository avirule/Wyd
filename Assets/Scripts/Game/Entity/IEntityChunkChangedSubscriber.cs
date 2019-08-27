namespace Game.Entity
{
    public interface IEntityChunkChangedSubscriber
    {
        bool EntityChangedChunk { get; set; }
    }
}