#region

using System;
using Wyd.Controllers.System;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class ChunkLoadTimeTextController : UpdatingFormattedTextController
    {
        private const double _TOLERANCE = 0.01d;

        private double _RecentNoiseRetrievalTime;
        private double _RecentTerrainGenerationTime;
        private double _RecentMeshingSetBlockTime;
        private double _RecentMeshingTime;

        protected override void Awake()
        {
            base.Awake();

            SetRecentAverages(0d, 0d, 0d, 0d);
            UpdateChunkLoadTimeText();
        }

        protected override void TimedUpdate()
        {
            double averageNoiseRetrievalTime = DiagnosticsController.Current.AverageNoiseRetrievalTime;
            double averageTerrainGenerationTime = DiagnosticsController.Current.AverageTerrainGenerationTime;
            double averageMeshingSetBlockTime = DiagnosticsController.Current.AverageMeshingSetBlockTime;
            double averageMeshingTime = DiagnosticsController.Current.AverageMeshingTime;

            if (!(Math.Abs(_RecentNoiseRetrievalTime - averageNoiseRetrievalTime) > _TOLERANCE)
                && !(Math.Abs(_RecentTerrainGenerationTime - averageTerrainGenerationTime) > _TOLERANCE)
                && !(Math.Abs(_RecentMeshingSetBlockTime - averageMeshingSetBlockTime) > _TOLERANCE)
                && !(Math.Abs(_RecentMeshingTime - averageMeshingTime) > _TOLERANCE))
            {
                return;
            }

            SetRecentAverages(
                averageNoiseRetrievalTime,
                averageTerrainGenerationTime,
                averageMeshingSetBlockTime,
                averageMeshingTime);

            UpdateChunkLoadTimeText();
        }

        private void SetRecentAverages(double averageNoiseRetrievalTime, double averageTerrainGenerationTime,
            double averageMeshingSetBlockTime, double averageMeshingTime)
        {
            _RecentNoiseRetrievalTime = averageNoiseRetrievalTime;
            _RecentTerrainGenerationTime = averageTerrainGenerationTime;
            _RecentMeshingSetBlockTime = averageMeshingSetBlockTime;
            _RecentMeshingTime = averageMeshingTime;
        }

        private void UpdateChunkLoadTimeText()
        {
            TextObject.text = string.Format(Format,
                _RecentNoiseRetrievalTime,
                _RecentTerrainGenerationTime,
                _RecentMeshingSetBlockTime,
                _RecentMeshingTime);
        }
    }
}
