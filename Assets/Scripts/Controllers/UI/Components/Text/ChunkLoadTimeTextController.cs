#region

using System;
using System.Linq;
using Game.World.Chunks;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunkLoadTimeTextController : MonoBehaviour
    {
        private const double _TOLERANCE = 0.001d;
        private TextMeshProUGUI _ChunkLoadTimeText;
        private double _LastBuildTime;
        private double _LastMeshTime;

        private void Awake()
        {
            _ChunkLoadTimeText = GetComponent<TextMeshProUGUI>();
            _ChunkLoadTimeText.text = "Chunk Load Time: (b0ms, m0ms)";
        }

        private void Update()
        {
            (double buildTime, double meshTime) = CalculateBuildAndMeshTimes();

            if ((Math.Abs(buildTime - _LastBuildTime) > _TOLERANCE) ||
                (Math.Abs(meshTime - _LastMeshTime) > _TOLERANCE))
            {
                UpdateChunkLoadTimeText(buildTime, meshTime);
            }
        }

        private void UpdateChunkLoadTimeText(double buildTime, double meshTime)
        {
            _LastBuildTime = buildTime;
            _LastMeshTime = meshTime;

            _ChunkLoadTimeText.text = $"Chunk Load Time: (b{buildTime}ms, m{meshTime}ms)";
        }

        private static (double, double) CalculateBuildAndMeshTimes()
        {
            double avgBuildTime = 0d;
            double avgMeshTime = 0d;

            if ((Chunk.BuildTimes != default) && (Chunk.BuildTimes.Count > 0))
            {
                avgBuildTime = Chunk.BuildTimes.Average(timeSpan => timeSpan.TotalMilliseconds);
            }

            if ((Chunk.MeshTimes != default) && (Chunk.MeshTimes.Count > 0))
            {
                avgMeshTime = Chunk.MeshTimes.Average(timeSpan => timeSpan.TotalMilliseconds);
            }

            double buildTime = Math.Round(avgBuildTime, 0);
            double meshTime = Math.Round(avgMeshTime, 0);

            return (buildTime, meshTime);
        }
    }
}