#region

using System;
using Wyd.Controllers.System;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class ChunkLoadTimeTextController : UpdatingFormattedTextController
    {
        private const double _TOLERANCE = 0.01d;

        private double _RecentNoiseRetrievalTime;
        private double _RecentTerrainBuildingTime;
        private double _RecentTerrainDetailingTime;
        private double _RecentPreMeshingTime;
        private double _RecentMeshingTime;

        protected override void TimedUpdate()
        {
            double averageNoiseRetrievalTime = DiagnosticsController.Current.AverageNoiseRetrievalTime;
            double averageTerrainBuildingTime = DiagnosticsController.Current.AverageTerrainBuildingTime;
            double averageTerrainDetailingTime = DiagnosticsController.Current.AverageTerrainDetailingTime;
            double averagePreMeshingTime = DiagnosticsController.Current.AverageMeshingPreMeshingTime;
            double averageMeshingTime = DiagnosticsController.Current.AverageMeshingTime;

            if ((Math.Abs(_RecentNoiseRetrievalTime - averageNoiseRetrievalTime) < _TOLERANCE)
                && (Math.Abs(_RecentTerrainBuildingTime - averageTerrainBuildingTime) < _TOLERANCE)
                && (Math.Abs(_RecentTerrainDetailingTime - averageTerrainDetailingTime) < _TOLERANCE)
                && (Math.Abs(_RecentPreMeshingTime - averagePreMeshingTime) < _TOLERANCE)
                && (Math.Abs(_RecentMeshingTime - averageMeshingTime) < _TOLERANCE))
            {
                return;
            }

            SetRecentAverages(
                averageNoiseRetrievalTime,
                averageTerrainBuildingTime,
                averageTerrainDetailingTime,
                averagePreMeshingTime,
                averageMeshingTime);

            UpdateChunkLoadTimeText();
        }

        private void SetRecentAverages(double averageNoiseRetrievalTime, double averageTerrainBuildingTime, double averageTerrainDetailingTime,
            double averagePreMeshingTime, double averageMeshingTime)
        {
            _RecentNoiseRetrievalTime = averageNoiseRetrievalTime;
            _RecentTerrainBuildingTime = averageTerrainBuildingTime;
            _RecentTerrainDetailingTime = averageTerrainDetailingTime;
            _RecentPreMeshingTime = averagePreMeshingTime;
            _RecentMeshingTime = averageMeshingTime;
        }

        private void UpdateChunkLoadTimeText()
        {
            _TextObject.text = string.Format(_Format,
                _RecentNoiseRetrievalTime,
                _RecentTerrainBuildingTime,
                _RecentTerrainDetailingTime,
                _RecentPreMeshingTime,
                _RecentMeshingTime);
        }
    }
}
