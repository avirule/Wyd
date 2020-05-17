#region

using ConcurrentAsyncScheduler;
using Wyd.Controllers.World;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class ChunksLoadedTextController : UpdatingFormattedTextController
    {
        private bool _UpdateText;

        protected override void OnEnable()
        {
            base.OnEnable();

            _UpdateText = true;

            AsyncJobScheduler.JobQueued += OnAsyncJobSchedulerEvent;
            AsyncJobScheduler.JobStarted += OnAsyncJobSchedulerEvent;
            AsyncJobScheduler.JobFinished += OnAsyncJobSchedulerEvent;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _UpdateText = false;

            AsyncJobScheduler.JobQueued -= OnAsyncJobSchedulerEvent;
            AsyncJobScheduler.JobStarted -= OnAsyncJobSchedulerEvent;
            AsyncJobScheduler.JobFinished -= OnAsyncJobSchedulerEvent;
        }

        protected override void TimedUpdate()
        {
            if (_UpdateText)
            {
                UpdateChunkRegionsLoadedText(WorldController.Current.ChunksQueuedCount, WorldController.Current.ChunksActiveCount,
                    WorldController.Current.ChunksCachedCount,
                    Singletons.Diagnostics.Instance.GetAverage("WorldStateVerification").TotalMilliseconds);
            }
        }

        private void UpdateChunkRegionsLoadedText(int chunksQueued, int chunksActive,
            int chunksCached, double averageStateVerificationTime)
        {
            _TextObject.text = string.Format(_Format, chunksQueued, chunksActive, chunksCached, averageStateVerificationTime);
        }

        private void OnAsyncJobSchedulerEvent(object _, AsyncJob __)
        {
            _UpdateText = true;
        }
    }
}
