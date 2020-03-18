#region

using System;
using Wyd.Controllers.State;
using Wyd.System.Collections;

#endregion

namespace Wyd.Controllers
{
    public class DiagnosticsController : SingletonController<DiagnosticsController>
    {
        public FixedConcurrentQueue<TimeSpan> RollingChunkBuildTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingChunkMeshTimes { get; private set; }

        public void Start()
        {
            RollingChunkBuildTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
            RollingChunkMeshTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
        }
    }
}
