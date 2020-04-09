#region

using System;
using System.Linq;
using Wyd.Controllers.State;
using Wyd.System.Collections;

#endregion

namespace Wyd.Controllers.System
{
    public class DiagnosticsController : SingletonController<DiagnosticsController>
    {
        public FixedConcurrentQueue<TimeSpan> RollingNoiseRetrievalTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingTerrainGenerationTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingMeshingSetBlockTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingMeshingTimes { get; private set; }

        public double AverageNoiseRetrievalTime => RollingNoiseRetrievalTimes.Count == 0
            ? 0d
            : RollingNoiseRetrievalTimes.Average(timeSpan => timeSpan.TotalMilliseconds);

        public double AverageTerrainGenerationTime => RollingTerrainGenerationTimes.Count == 0
            ? 0d
            : RollingTerrainGenerationTimes.Average(timeSpan => timeSpan.TotalMilliseconds);

        public double AverageMeshingSetBlockTime => RollingMeshingSetBlockTimes.Count == 0
            ? 0d
            : RollingMeshingSetBlockTimes.Average(timeSpan => timeSpan.TotalMilliseconds);

        public double AverageMeshingTime => RollingMeshingTimes.Count == 0
            ? 0d
            : RollingMeshingTimes.Average(timeSpan => timeSpan.TotalMilliseconds);

        private void Start()
        {
            AssignSingletonInstance(this);

            RollingTerrainGenerationTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
            RollingNoiseRetrievalTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
            RollingMeshingTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
            RollingMeshingSetBlockTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
        }
    }
}
