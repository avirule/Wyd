#region

using System;
using Wyd.Controllers.State;
using Wyd.System.Collections;

#endregion

namespace Wyd.Controllers.System
{
    public class DiagnosticsController : SingletonController<DiagnosticsController>
    {
        public FixedConcurrentQueue<TimeSpan> RollingChunkNoiseRetrievalTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingTotalChunkBuildTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingChunkMeshSetBlockTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingTotalChunkMeshTimes { get; private set; }

        private void Start()
        {
            AssignSingletonInstance(this);

            RollingTotalChunkBuildTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
            RollingChunkNoiseRetrievalTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);

            RollingTotalChunkMeshTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
            RollingChunkMeshSetBlockTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
        }
    }
}
