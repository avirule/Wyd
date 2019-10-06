#region

using Controllers.World;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunksLoadedTextController : FormattedTextController
    {
        private int _LastQueuedForCreationCount;
        private int _LastChunkRegionsActiveCount;
        private int _LastChunkRegionsCachedCount;

        private void Start()
        {
            int chunksQueuedForCreation = WorldController.Current.ChunkRegionsQueuedForCreation;
            int chunksActive = WorldController.Current.ChunkRegionsActiveCount;
            int chunksCached = WorldController.Current.ChunkRegionsCachedCount;

            UpdateChunkRegionsLoadedText(chunksQueuedForCreation, chunksActive, chunksCached);
        }

        private void Update()
        {
            int chunksQueuedForCreation = WorldController.Current.ChunkRegionsQueuedForCreation;
            int chunksActive = WorldController.Current.ChunkRegionsActiveCount;
            int chunksCached = WorldController.Current.ChunkRegionsCachedCount;

            if ((chunksQueuedForCreation != _LastQueuedForCreationCount)
                || (chunksActive != _LastChunkRegionsActiveCount)
                || (chunksCached != _LastChunkRegionsCachedCount))
            {
                UpdateChunkRegionsLoadedText(chunksQueuedForCreation, chunksActive, chunksCached);
            }
        }

        private void UpdateChunkRegionsLoadedText(int chunksQueuedForCreation, int chunksActive,
            int chunksCached)
        {
            _LastQueuedForCreationCount = chunksQueuedForCreation;
            _LastChunkRegionsActiveCount = chunksActive;
            _LastChunkRegionsCachedCount = chunksCached;

            TextObject.text = string.Format(Format,
                _LastQueuedForCreationCount,
                _LastChunkRegionsActiveCount,
                _LastChunkRegionsCachedCount);
        }
    }
}
