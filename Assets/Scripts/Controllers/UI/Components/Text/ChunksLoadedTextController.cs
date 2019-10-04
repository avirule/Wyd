#region

using Controllers.World;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunksLoadedTextController : FormattedTextController
    {
        private int _LastQueuedForCreationCount;
        private int _LastChunksActiveCount;
        private int _LastChunksCachedCount;

        private void Start()
        {
            int chunksQueuedForCreation = WorldController.Current.ChunksQueuedForCreation;
            int chunksActive = WorldController.Current.ChunksActiveCount;
            int chunksCached = WorldController.Current.ChunksCachedCount;

            UpdateChunksLoadedText(chunksQueuedForCreation, chunksActive, chunksCached);
        }

        private void Update()
        {
            int chunksQueuedForCreation = WorldController.Current.ChunksQueuedForCreation;
            int chunksActive = WorldController.Current.ChunksActiveCount;
            int chunksCached = WorldController.Current.ChunksCachedCount;

            if ((chunksQueuedForCreation != _LastQueuedForCreationCount)
                || (chunksActive != _LastChunksActiveCount)
                || (chunksCached != _LastChunksCachedCount))
            {
                UpdateChunksLoadedText(chunksQueuedForCreation, chunksActive, chunksCached);
            }
        }

        private void UpdateChunksLoadedText(int chunksQueuedForCreation, int chunksActive, int chunksCached)
        {
            _LastQueuedForCreationCount = chunksQueuedForCreation;
            _LastChunksActiveCount = chunksActive;
            _LastChunksCachedCount = chunksCached;

            TextObject.text = string.Format(Format, _LastQueuedForCreationCount, _LastChunksActiveCount,
                _LastChunksCachedCount);
        }
    }
}
