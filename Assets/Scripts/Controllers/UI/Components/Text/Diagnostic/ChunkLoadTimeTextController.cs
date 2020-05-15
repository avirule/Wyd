#region

using System;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class ChunkLoadTimeTextController : UpdatingFormattedTextController
    {
        private bool _UpdateDisplayData;

        protected override void OnEnable()
        {
            base.OnEnable();

            Singletons.Diagnostics.Instance.DiagnosticBuffersChanged += OnDiagnosticBuffersChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Singletons.Diagnostics.Instance.DiagnosticBuffersChanged -= OnDiagnosticBuffersChanged;
        }

        protected override void TimedUpdate()
        {
            if (_UpdateDisplayData)
            {
                SetAverages(
                    Singletons.Diagnostics.Instance.GetAverage("ChunkNoiseRetrieval"),
                    Singletons.Diagnostics.Instance.GetAverage("ChunkBuilding"),
                    Singletons.Diagnostics.Instance.GetAverage("ChunkDetailing"),
                    Singletons.Diagnostics.Instance.GetAverage("ChunkPreMeshing"),
                    Singletons.Diagnostics.Instance.GetAverage("ChunkMeshing"));
            }
        }

        private void SetAverages(TimeSpan averageNoiseRetrievalTime, TimeSpan averageTerrainBuildingTime, TimeSpan averageTerrainDetailingTime,
            TimeSpan averagePreMeshingTime, TimeSpan averageMeshingTime)
        {
            _TextObject.text = string.Format(_Format,
                averageNoiseRetrievalTime.Milliseconds,
                averageTerrainBuildingTime.Milliseconds,
                averageTerrainDetailingTime.Milliseconds,
                averagePreMeshingTime.Milliseconds,
                averageMeshingTime.Milliseconds);
        }

        private void OnDiagnosticBuffersChanged(object sender, TimeSpan changedTimeSpan)
        {
            _UpdateDisplayData = true;
        }
    }
}
