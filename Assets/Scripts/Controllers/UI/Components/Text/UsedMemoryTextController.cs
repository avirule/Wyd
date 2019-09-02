#region

using System;
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
        private DateTime _LastCheckedDisplayedAccuracyTime;
        private long _LastReservedMemoryTotal;
        private long _LastAllocatedMemoryTotal;

        public int Precision = 1;

        private void Awake()
        {
            _UsedMemoryText = GetComponent<TextMeshProUGUI>();
            _UsedMemoryText.text = "Used Memory: (r0MB, a0MB)";
            _MaximumDisplayAccuracyUpdateInterval = TimeSpan.FromSeconds(1d / 2);
            _LastCheckedDisplayedAccuracyTime = DateTime.MinValue;
        }

        private void Update()
        {
            if ((DateTime.Now - _LastCheckedDisplayedAccuracyTime) < _MaximumDisplayAccuracyUpdateInterval)
            {
                return;
            }

            (long totalReservedMemory, long totalAllocatedMemory) = GetUsedMemory();

            if ((totalReservedMemory != _LastReservedMemoryTotal) ||
                (totalAllocatedMemory != _LastAllocatedMemoryTotal))
            {
                UpdateUsedMemoryText(totalReservedMemory, totalAllocatedMemory);
            }
        }

        private void UpdateUsedMemoryText(long reservedMemory, long allocatedMemory)
        {
            _LastReservedMemoryTotal = reservedMemory;
            _LastAllocatedMemoryTotal = allocatedMemory;

            double reservedMemoryInMb = Math.Round(reservedMemory / _MEGABYTE_VALUE, Precision);
            double allocatedMemoryInMb = Math.Round(allocatedMemory / _MEGABYTE_VALUE, Precision);

            _UsedMemoryText.text = $"Used Memory: (r{reservedMemoryInMb}MB, a{allocatedMemoryInMb}MB)";

            _LastCheckedDisplayedAccuracyTime = DateTime.Now;
        }

        private static (long, long) GetUsedMemory()
        {
            return (Profiler.GetTotalReservedMemoryLong(), Profiler.GetTotalAllocatedMemoryLong());
        }
    }
}