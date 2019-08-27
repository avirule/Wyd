#region

using System;
using System.Collections.Concurrent;
using System.Linq;
using Controllers.Game;
using Controllers.World;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

#endregion

namespace Controllers.UI.Diagnostics
{
    public class DiagnosticsPanelController : MonoBehaviour
    {
        public static readonly ConcurrentQueue<double> ChunkBuildTimes = new ConcurrentQueue<double>();
        public static readonly ConcurrentQueue<double> ChunkMeshTimes = new ConcurrentQueue<double>();

        public TextMeshProUGUI ChunkLoadTime;
        public TextMeshProUGUI ChunksActive;
        public TextMeshProUGUI ChunksCached;

        public TextMeshProUGUI ReservedMemory;
        public TextMeshProUGUI AllocatedMemory;


        // Start is called before the first frame update
        private void Start()
        {
            gameObject.SetActive(Debug.isDebugBuild);
        }

        public void Update()
        {
            CullChunkLoadTimesQueue();

            if (!gameObject.activeSelf)
            {
                return;
            }

            UpdateFramesText();
            UpdateResourcesText();
        }

        private void UpdateFramesText()
        {
            double avgBuildTime = ChunkBuildTimes.Count > 0 ? ChunkBuildTimes.Average() : 0d;
            double avgMeshTime = ChunkMeshTimes.Count > 0 ? ChunkMeshTimes.Average() : 0d;
            
            double buildTime = Math.Round(avgBuildTime, 0);
            double meshTime = Math.Round(avgMeshTime, 0);

            ChunkLoadTime.text = $"Chunk Load Time: (b{buildTime}ms, m{meshTime}ms)";
            ChunksActive.text = $"Chunks Active: {ChunkController.Current.ActiveChunksCount}";
            ChunksCached.text = $"Chunks Cached: {ChunkController.Current.CachedChunksCount}";
        }

        private void CullChunkLoadTimesQueue()
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

            ReservedMemory.text = $"Reserv Mem: {adjustedTotalReservedMemory}MB";
            AllocatedMemory.text = $"Alloc Mem: {adjustedTotalAllocatedMemory}MB";
        }
    }
}