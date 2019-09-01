#region

using System;
using Controllers.World;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

#endregion

namespace Controllers.UI.Diagnostics
{
    public class DiagnosticsPanelController : MonoBehaviour
    {
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
            if (!gameObject.activeSelf)
            {
                return;
            }

            UpdateFramesText();
            UpdateResourcesText();
        }

        private void UpdateFramesText()
        {
            ChunksActive.text = $"Chunks Active: {ChunkController.Current.ActiveChunksCount}";
            ChunksCached.text = $"Chunks Cached: {ChunkController.Current.CachedChunksCount}";
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