#region

using System;
using System.Linq;
using Wyd.Game.World.Chunks;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class ChunkLoadTimeTextController : UpdatingFormattedTextController
    {
        private const double TOLERANCE = 0.001d;

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

            if ((ChunkGenerator.BuildTimes != default) && (ChunkGenerator.BuildTimes.Count > 0))
            {
                avgBuildTime = ChunkGenerator.BuildTimes.Average(timeSpan => timeSpan.TotalMilliseconds);
            }

            if ((ChunkGenerator.MeshTimes != default) && (ChunkGenerator.MeshTimes.Count > 0))
            {
                avgMeshTime = ChunkGenerator.MeshTimes.Average(timeSpan => timeSpan.TotalMilliseconds);
            }

            return (avgBuildTime, avgMeshTime);
        }
    }
}
