#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Game;
using Controllers.World;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

#endregion

namespace Controllers.UI
{
    public class DiagnosticsController : MonoBehaviour
    {
        private const double _LOCAL_FRAME_INTERVAL = 1f / 15f;
        private Stopwatch _LocalFrameStopwatch;
        private double _DeltaTimeAverage;
        private List<double> _DeltaTimes;

        public static readonly ConcurrentQueue<double> ChunkBuildTimes = new ConcurrentQueue<double>();
        public static readonly ConcurrentQueue<double> ChunkMeshTimes = new ConcurrentQueue<double>();

        public Text FrameRateText;
        public Text ResourcesText;
        public Text VersionText;

        private void Awake()
        {
            VersionText.text = Application.version;
            _LocalFrameStopwatch = new Stopwatch();
            _DeltaTimes = new List<double>();
        }

        // Start is called before the first frame update
        private void Start()
        {
            gameObject.SetActive(Debug.isDebugBuild);
            _LocalFrameStopwatch.Start();
        }

        public void Update()
        {
            if (_LocalFrameStopwatch.Elapsed.TotalSeconds < _LOCAL_FRAME_INTERVAL)
            {
                return;
            }

            _LocalFrameStopwatch.Restart();

            UpdateDeltaTimes();
            CullChunkLoadQueue();

            if (!gameObject.activeSelf)
            {
                return;
            }

            UpdateFramesText();
            UpdateResourcesText();
        }


        private void UpdateDeltaTimes()
        {
            _DeltaTimes.Add(1d / Time.deltaTime);

            if (_DeltaTimes.Count > GameController.SettingsController.MaximumFrameRateCacheSize)
            {
                _DeltaTimes.RemoveRange(0,
                    _DeltaTimes.Count - GameController.SettingsController.MaximumFrameRateCacheSize);
            }

            _DeltaTimeAverage = Math.Round(_DeltaTimes.Average(), 4);
        }

        private void UpdateFramesText()
        {
            string vSyncStatus = QualitySettings.vSyncCount == 0 ? "Disabled" : "Enabled";

            double sumLoadTimes = ChunkBuildTimes.Sum() + ChunkMeshTimes.Sum();
            double averageLoadTime = Math.Round(sumLoadTimes / (ChunkBuildTimes.Count + ChunkMeshTimes.Count));

            FrameRateText.text =
                $"FPS: {_DeltaTimeAverage}\r\n" +
                $"VSync: {vSyncStatus}\r\n" +
                $"Chunk Load Time: {averageLoadTime}ms\r\n" +
                $"Chunks Cached: {ChunkController.Current.CurrentCacheSize}";
        }

        private void CullChunkLoadQueue()
        {
            while (ChunkMeshTimes.Count > GameController.SettingsController.MaximumChunkLoadTimeCacheSize)
            {
                ChunkMeshTimes.TryDequeue(out double _);
            }

            while (ChunkMeshTimes.Count > GameController.SettingsController.MaximumChunkLoadTimeCacheSize)
            {
                ChunkMeshTimes.TryDequeue(out double _);
            }
        }

        private void UpdateResourcesText()
        {
            long totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            double adjustedTotalAllocatedMemory = Math.Round(totalAllocatedMemory / 1000000d, 2);

            long totalReservedMemory = Profiler.GetTotalReservedMemoryLong();
            double adjustedTotalReservedMemory = Math.Round(totalReservedMemory / 1000000d, 2);

            ResourcesText.text =
                $"Reserv Mem: {adjustedTotalReservedMemory}MB\r\nAlloc Mem: {adjustedTotalAllocatedMemory}MB";
        }
    }
}