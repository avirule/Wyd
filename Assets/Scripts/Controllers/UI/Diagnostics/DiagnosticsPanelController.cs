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

namespace Controllers.UI.Diagnostics
{
    public class DiagnosticsPanelController : MonoBehaviour
    {
        private const double _LOCAL_FRAME_INTERVAL = 1f / 15f;
        private Stopwatch _LocalFrameStopwatch;
        private double _DeltaTimeAverage;
        private List<double> _DeltaTimes;

        public static readonly ConcurrentQueue<double> ChunkBuildTimes = new ConcurrentQueue<double>();
        public static readonly ConcurrentQueue<double> ChunkMeshTimes = new ConcurrentQueue<double>();

        public Text FrameRateText;
        public Text ResourcesText;

        private void Awake()
        {
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

            if (_DeltaTimes.Count > OptionsController.Current.MaximumFrameRateBufferSize)
            {
                _DeltaTimes.RemoveRange(0,
                    _DeltaTimes.Count - OptionsController.Current.MaximumFrameRateBufferSize);
            }

            _DeltaTimeAverage = Math.Round(_DeltaTimes.Average(), 4);
        }

        private void UpdateFramesText()
        {
            double sumLoadTimes = ChunkBuildTimes.Sum() + ChunkMeshTimes.Sum();
            double averageLoadTime = Math.Round(sumLoadTimes / (ChunkBuildTimes.Count + ChunkMeshTimes.Count));

            FrameRateText.text =
                $"FPS: {_DeltaTimeAverage}\r\n" +
                $"VSync Level: {QualitySettings.vSyncCount}\r\n" +
                $"Chunk Load Time: {averageLoadTime}ms\r\n" +
                $"Chunks Active: {ChunkController.Current.ActiveChunksCount}\r\n" +
                $"Chunks Cached: {ChunkController.Current.CachedChunksCount}\r\n";
        }

        private void CullChunkLoadQueue()
        {
            while (ChunkMeshTimes.Count > OptionsController.Current.MaximumChunkLoadTimeBufferSize)
            {
                ChunkMeshTimes.TryDequeue(out double _);
            }

            while (ChunkMeshTimes.Count > OptionsController.Current.MaximumChunkLoadTimeBufferSize)
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