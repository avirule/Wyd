#region

using System;
using System.Linq;
using Game.World.Chunks;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunkLoadTimeTextController : FormattedTextController
    {
        private const double TOLERANCE = 0.001d;

        private int _SkippedFrames;
        private double _LastBuildTime;
        private double _LastMeshTime;

        public int SkipFrames = 30;

        private void Update()
        {
            if (_SkippedFrames <= SkipFrames)
            {
                _SkippedFrames += 1;
                return;
            }

            _SkippedFrames = 0;

            (double buildTime, double meshTime) = CalculateBuildAndMeshTimes();

            if ((Math.Abs(buildTime - _LastBuildTime) > TOLERANCE)
                || (Math.Abs(meshTime - _LastMeshTime) > TOLERANCE))
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

            if ((ChunkGenerationDispatcher.BuildTimes != default) && (ChunkGenerationDispatcher.BuildTimes.Count > 0))
            {
                avgBuildTime = ChunkGenerationDispatcher.BuildTimes.Average(timeSpan => timeSpan.TotalMilliseconds);
            }

            if ((ChunkGenerationDispatcher.MeshTimes != default) && (ChunkGenerationDispatcher.MeshTimes.Count > 0))
            {
                avgMeshTime = ChunkGenerationDispatcher.MeshTimes.Average(timeSpan => timeSpan.TotalMilliseconds);
            }

            return (avgBuildTime, avgMeshTime);
        }
    }
}
