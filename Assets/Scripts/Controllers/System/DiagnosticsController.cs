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
        public FixedConcurrentQueue<TimeSpan> RollingTerrainBuildingTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingTerrainDetailingTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingMeshingSetBlockTimes { get; private set; }
        public FixedConcurrentQueue<TimeSpan> RollingMeshingTimes { get; private set; }

        public double AverageNoiseRetrievalTime => RollingNoiseRetrievalTimes.Count > 0
            ? RollingNoiseRetrievalTimes.Average(timeSpan => timeSpan.TotalMilliseconds)
            : 0d;

        public double AverageTerrainBuildingTime => RollingTerrainBuildingTimes.Count > 0
            ? RollingTerrainBuildingTimes.Average(timeSpan => timeSpan.TotalMilliseconds)
            : 0d;

        public double AverageTerrainDetailingTime => RollingTerrainDetailingTimes.Count > 0
            ? RollingTerrainDetailingTimes.Average(timeSpan => timeSpan.TotalMilliseconds)
            : 0d;

        public double AverageMeshingSetBlockTime => RollingMeshingSetBlockTimes.Count > 0
            ? RollingMeshingSetBlockTimes.Average(timeSpan => timeSpan.TotalMilliseconds)
            : 0d;

        public double AverageMeshingTime => RollingMeshingTimes.Count > 0
            ? RollingMeshingTimes.Average(timeSpan => timeSpan.TotalMilliseconds)
            : 0d;

        private void Start()
        {
            AssignSingletonInstance(this);

            RollingNoiseRetrievalTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
            RollingTerrainBuildingTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
            RollingTerrainDetailingTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
            RollingMeshingSetBlockTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
            RollingMeshingTimes =
                new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumDiagnosticBuffersSize);
        }
    }
}
