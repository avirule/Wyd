#region

using System;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

#endregion

namespace Controllers.UI.Components.Text
{
    public class UsedMemoryTextController : MonoBehaviour
    {
        private const double _MEGABYTE_VALUE = 1000000d;

        private TextMeshProUGUI _UsedMemoryText;
        private TimeSpan _MaximumDisplayAccuracyUpdateInterval;
        private Stopwatch _LastUpdateTimer;
        private long _LastReservedMemoryTotal;
        private long _LastAllocatedMemoryTotal;

        public int Precision = 1;

        private void Awake()
        {
            _UsedMemoryText = GetComponent<TextMeshProUGUI>();
            _UsedMemoryText.text = "Used Memory: (r0MB, a0MB)";
            _MaximumDisplayAccuracyUpdateInterval = TimeSpan.FromSeconds(1d / 2);
            _LastUpdateTimer = Stopwatch.StartNew();
        }

        private void Update()
        {
            if (_LastUpdateTimer.Elapsed <= _MaximumDisplayAccuracyUpdateInterval)
            {
                return;
            }

            (long totalReservedMemory, long totalAllocatedMemory) = GetUsedMemory();

            if ((totalReservedMemory != _LastReservedMemoryTotal) ||
                (totalAllocatedMemory != _LastAllocatedMemoryTotal))
            {
                UpdateUsedMemoryText(totalReservedMemory, totalAllocatedMemory);
            }

            _LastUpdateTimer.Restart();
        }

        private void UpdateUsedMemoryText(long reservedMemory, long allocatedMemory)
        {
            _LastReservedMemoryTotal = reservedMemory;
            _LastAllocatedMemoryTotal = allocatedMemory;

            double reservedMemoryInMb = Math.Round(reservedMemory / _MEGABYTE_VALUE, Precision);
            double allocatedMemoryInMb = Math.Round(allocatedMemory / _MEGABYTE_VALUE, Precision);

            _UsedMemoryText.text = $"Used Memory: (r{reservedMemoryInMb}MB, a{allocatedMemoryInMb}MB)";
        }

        private static (long, long) GetUsedMemory()
        {
            return (Profiler.GetTotalReservedMemoryLong(), Profiler.GetTotalAllocatedMemoryLong());
        }
    }
}