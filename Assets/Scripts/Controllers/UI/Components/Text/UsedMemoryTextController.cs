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
        private const double MEGABYTE_VALUE = 1000000d;

        private string _Format;
        private TextMeshProUGUI _UsedMemoryText;
        private TimeSpan _MaximumDisplayAccuracyUpdateInterval;
        private Stopwatch _LastUpdateTimer;
        private long _LastReservedMemoryTotal;
        private long _LastAllocatedMemoryTotal;

        public int Precision = 1;

        private void Awake()
        {
            _UsedMemoryText = GetComponent<TextMeshProUGUI>();
            _Format = _UsedMemoryText.text;
            _MaximumDisplayAccuracyUpdateInterval = TimeSpan.FromSeconds(1d / 2d);
            _LastUpdateTimer = Stopwatch.StartNew();
        }

        private void Update()
        {
            if (_LastUpdateTimer.Elapsed <= _MaximumDisplayAccuracyUpdateInterval)
            {
                return;
            }

            (long totalReservedMemory, long totalAllocatedMemory) = GetUsedMemory();

            if ((totalReservedMemory != _LastReservedMemoryTotal)
                || (totalAllocatedMemory != _LastAllocatedMemoryTotal))
            {
                UpdateUsedMemoryText(totalReservedMemory, totalAllocatedMemory);
            }

            _LastUpdateTimer.Restart();
        }

        private void UpdateUsedMemoryText(long reservedMemory, long allocatedMemory)
        {
            _LastReservedMemoryTotal = reservedMemory;
            _LastAllocatedMemoryTotal = allocatedMemory;

            double reservedMemoryInMb = Math.Round(reservedMemory / MEGABYTE_VALUE, Precision);
            double allocatedMemoryInMb = Math.Round(allocatedMemory / MEGABYTE_VALUE, Precision);

            _UsedMemoryText.text = string.Format(_Format, reservedMemoryInMb, allocatedMemoryInMb,
                (allocatedMemoryInMb / reservedMemoryInMb) * 100d);
        }

        private static (long, long) GetUsedMemory()
        {
            return (Profiler.GetTotalReservedMemoryLong(), Profiler.GetTotalAllocatedMemoryLong());
        }
    }
}
