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
        private double _RecentMeshingSetBlockTime;
        private double _RecentMeshingTime;

        protected override void TimedUpdate()
        {
            double averageNoiseRetrievalTime = DiagnosticsController.Current.AverageNoiseRetrievalTime;
            double averageTerrainBuildingTime = DiagnosticsController.Current.AverageTerrainBuildingTime;
            double averageTerrainDetailingTime = DiagnosticsController.Current.AverageTerrainDetailingTime;
            double averageMeshingSetBlockTime = DiagnosticsController.Current.AverageMeshingSetBlockTime;
            double averageMeshingTime = DiagnosticsController.Current.AverageMeshingTime;

            if ((Math.Abs(_RecentNoiseRetrievalTime - averageNoiseRetrievalTime) < _TOLERANCE)
                && (Math.Abs(_RecentTerrainBuildingTime - averageTerrainBuildingTime) < _TOLERANCE)
                && (Math.Abs(_RecentTerrainDetailingTime - averageTerrainDetailingTime) < _TOLERANCE)
                && (Math.Abs(_RecentMeshingSetBlockTime - averageMeshingSetBlockTime) < _TOLERANCE)
                && (Math.Abs(_RecentMeshingTime - averageMeshingTime) < _TOLERANCE))
            {
                return;
            }

            SetRecentAverages(
                averageNoiseRetrievalTime,
                averageTerrainBuildingTime,
                averageTerrainDetailingTime,
                averageMeshingSetBlockTime,
                averageMeshingTime);

            UpdateChunkLoadTimeText();
        }

        private void SetRecentAverages(double averageNoiseRetrievalTime, double averageTerrainBuildingTime, double averageTerrainDetailingTime, double averageMeshingSetBlockTime, double averageMeshingTime)
        {
            _RecentNoiseRetrievalTime = averageNoiseRetrievalTime;
            _RecentTerrainBuildingTime = averageTerrainBuildingTime;
            _RecentTerrainDetailingTime = averageTerrainDetailingTime;
            _RecentMeshingSetBlockTime = averageMeshingSetBlockTime;
            _RecentMeshingTime = averageMeshingTime;
        }

        private void UpdateChunkLoadTimeText()
        {
            _TextObject.text = string.Format(_Format,
                _RecentNoiseRetrievalTime,
                _RecentTerrainBuildingTime,
                _RecentTerrainDetailingTime,
                _RecentMeshingSetBlockTime,
                _RecentMeshingTime);
        }
    }
}
