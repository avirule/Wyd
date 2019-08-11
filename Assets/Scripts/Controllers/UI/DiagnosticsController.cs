﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using Environment.Terrain;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

#endregion

namespace Controllers.UI
{
    public class DiagnosticsController : MonoBehaviour
    {
        private double _DeltaTimeAverage;
        private List<double> _DeltaTimes;


        public Text FrameRateText;

        public int MaximumFrameRateCaching;
        public Text ResourcesText;

        private void Awake()
        {
            _DeltaTimes = new List<double>();
        }

        // Start is called before the first frame update
        private void Start()
        {
            gameObject.SetActive(Debug.isDebugBuild);
        }

        public void Update()
        {
            UpdateDeltaTimes();
        }

        // Update is called once per frame
        private void FixedUpdate()
        {
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

            if (_DeltaTimes.Count > MaximumFrameRateCaching)
            {
                _DeltaTimes.RemoveRange(0, _DeltaTimes.Count - MaximumFrameRateCaching);
            }

            _DeltaTimeAverage = Math.Round(_DeltaTimes.Average(), 5);
        }

        private void UpdateFramesText()
        {
            string vSyncStatus = QualitySettings.vSyncCount == 0 ? "Disabled" : "Enabled";

            float sumLoadTimes = Chunk.ChunkBuildTimes.Sum() + Chunk.ChunkMeshTimes.Sum();
            float averageLoadTime =
                Mathf.Floor(sumLoadTimes / (Chunk.ChunkBuildTimes.Count + Chunk.ChunkMeshTimes.Count));

            FrameRateText.text =
                $"FPS: {_DeltaTimeAverage}\r\nVSync: {vSyncStatus}\r\nChunk Load Time: {averageLoadTime}ms";
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