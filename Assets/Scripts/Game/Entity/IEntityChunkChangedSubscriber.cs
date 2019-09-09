namespace Game.Entity
{
    public interface IEntityChunkChangedSubscriber
    {
        bool PrimaryLoaderChangedChunk { get; set; }
    }
}
