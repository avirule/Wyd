#region

using System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Controllers.State
{
    public class DiagnosticsController : SingletonController<DiagnosticsController>
    {
        public FixedConcurrentQueue<TimeSpan> RollingChunkBuildTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingChunkMeshTimes { get; private set; }

        private void Awake()
        {
            AssignCurrent(this);

            RollingChunkBuildTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
            RollingChunkMeshTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
        }
    }
}
