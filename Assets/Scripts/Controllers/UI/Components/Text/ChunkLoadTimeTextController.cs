#region

using System;
using System.Linq;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class ChunkLoadTimeTextController : UpdatingFormattedTextController
    {
        private const double _TOLERANCE = 0.001d;

        private double _LastBuildTime;
        private double _LastMeshTime;


        protected override void Awake()
        {
            base.Awake();

            _LastBuildTime = _LastMeshTime = -1d;
        }

        protected override void TimedUpdate()
        {
            (double buildTime, double meshTime) = CalculateBuildAndMeshTimes();

            if ((Math.Abs(buildTime - _LastBuildTime) > _TOLERANCE)
                || (Math.Abs(meshTime - _LastMeshTime) > _TOLERANCE))
            {
                UpdateChunkLoadTimeText(buildTime, meshTime);
            }
        }

        private void UpdateChunkLoadTimeText(double buildTime, double meshTime)
        {
            _LastBuildTime = buildTime;
            _LastMeshTime = meshTime;

            TextObject.text = string.Format(Format, buildTime, meshTime);
        }

        private static (double, double) CalculateBuildAndMeshTimes()
        {
            double avgBuildTime = 0d;
            double avgMeshTime = 0d;

            if ((DiagnosticsController.Current.RollingChunkBuildTimes != default)
                && (DiagnosticsController.Current.RollingChunkBuildTimes.Count > 0))
            {
                avgBuildTime =
                    DiagnosticsController.Current.RollingChunkBuildTimes.Average(timeSpan =>
                        timeSpan.TotalMilliseconds);
            }

            if ((DiagnosticsController.Current.RollingChunkMeshTimes != default)
                && (DiagnosticsController.Current.RollingChunkMeshTimes.Count > 0))
            {
                avgMeshTime =
                    DiagnosticsController.Current.RollingChunkMeshTimes.Average(timeSpan => timeSpan.TotalMilliseconds);
            }

            return (avgBuildTime, avgMeshTime);
        }
    }
}
