using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace Controllers.UI
{
    public class DiagnosticsController : MonoBehaviour
    {
        private double _DeltaTimeAverage;
        private Queue<double> _DeltaTimes;


        public Text FrameRateText;

        public int MaximumFrameRateCaching;
        public Text ResourcesText;

        private void Awake()
        {
            _DeltaTimes = new Queue<double>();
        }

        // Start is called before the first frame update
        private void Start()
        {
            gameObject.SetActive(Debug.isDebugBuild);
        }

        // Update is called once per frame
        private void Update()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            UpdateDeltaTimes();
            UpdateFramesText();
            UpdateResourcesText();
        }


        private void UpdateDeltaTimes()
        {
            _DeltaTimes.Enqueue(1d / Time.deltaTime);

            while (_DeltaTimes.Count > MaximumFrameRateCaching)
            {
                _DeltaTimes.Dequeue();
            }

            _DeltaTimeAverage = Math.Round(_DeltaTimes.Average(), 5);
        }

        private void UpdateFramesText()
        {
            string vSyncStatus = QualitySettings.vSyncCount == 0 ? "Disabled" : "Enabled";

            FrameRateText.text = $"FPS: {_DeltaTimeAverage}\r\nVSync: {vSyncStatus}";
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