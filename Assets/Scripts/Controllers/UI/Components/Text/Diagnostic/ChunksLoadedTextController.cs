#region

using Wyd.Controllers.World;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class ChunksLoadedTextController : UpdatingFormattedTextController
    {
        private int _LastQueuedForCreationCount;
        private int _LastChunkRegionsActiveCount;
        private int _LastChunkRegionsCachedCount;

        private void Start()
        {
            int chunksQueued = WorldController.Current.ChunksQueuedCount;
            int chunksActive = WorldController.Current.ChunksActiveCount;
            int chunksCached = WorldController.Current.ChunksCachedCount;

            UpdateChunkRegionsLoadedText(chunksQueued, chunksActive, chunksCached,
                WorldController.Current.AverageChunkStateVerificationTime);
        }

        protected override void TimedUpdate()
        {
            int chunksQueued = WorldController.Current.ChunksQueuedCount;
            int chunksActive = WorldController.Current.ChunksActiveCount;
            int chunksCached = WorldController.Current.ChunksCachedCount;

            if ((chunksQueued != _LastQueuedForCreationCount)
                || (chunksActive != _LastChunkRegionsActiveCount)
                || (chunksCached != _LastChunkRegionsCachedCount))
            {
                UpdateChunkRegionsLoadedText(chunksQueued, chunksActive, chunksCached,
                    WorldController.Current.AverageChunkStateVerificationTime);
            }
        }

        private void UpdateChunkRegionsLoadedText(int chunksQueuedForCreation, int chunksActive,
            int chunksCached, double averageStateVerificationTime)
        {
            _LastQueuedForCreationCount = chunksQueuedForCreation;
            _LastChunkRegionsActiveCount = chunksActive;
            _LastChunkRegionsCachedCount = chunksCached;

            TextObject.text = string.Format(Format,
                _LastQueuedForCreationCount,
                _LastChunkRegionsActiveCount,
                _LastChunkRegionsCachedCount,
                averageStateVerificationTime);
        }
    }
}
