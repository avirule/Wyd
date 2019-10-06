#region

using Controllers.World;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunkRegionsLoadedTextController : FormattedTextController
    {
        private int _LastQueuedForCreationCount;
        private int _LastChunkRegionsActiveCount;
        private int _LastChunkRegionsCachedCount;

        private void Start()
        {
            int chunkRegionsQueuedForCreation = WorldController.Current.ChunkRegionsQueuedForCreation;
            int chunkRegionsActive = WorldController.Current.ChunkRegionsActiveCount;
            int chunkRegionsCached = WorldController.Current.ChunkRegionsCachedCount;

            UpdateChunkRegionsLoadedText(chunkRegionsQueuedForCreation, chunkRegionsActive, chunkRegionsCached);
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

        private void UpdateChunkRegionsLoadedText(int chunkRegionsQueuedForCreation, int chunkRegionsActive,
            int chunkRegionsCached)
        {
            _LastQueuedForCreationCount = chunkRegionsQueuedForCreation;
            _LastChunkRegionsActiveCount = chunkRegionsActive;
            _LastChunkRegionsCachedCount = chunkRegionsCached;

            TextObject.text = string.Format(Format,
                _LastQueuedForCreationCount,
                _LastChunkRegionsActiveCount,
                _LastChunkRegionsCachedCount);
        }
    }
}
