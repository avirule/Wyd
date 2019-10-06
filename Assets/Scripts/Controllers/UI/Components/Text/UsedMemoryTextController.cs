#region

using System;
using System.Diagnostics;
using UnityEngine.Profiling;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class UsedMemoryTextController : FormattedTextController
    {
        private const double MEGABYTE_VALUE = 1000000d;

        private TimeSpan _MaximumDisplayAccuracyUpdateInterval;
        private Stopwatch _LastUpdateTimer;
        private long _LastReservedMemoryTotal;
        private long _LastAllocatedMemoryTotal;

        public int Precision = 1;

        protected override void Awake()
        {
            base.Awake();

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

            TextObject.text = string.Format(Format, reservedMemoryInMb, allocatedMemoryInMb,
                (allocatedMemoryInMb / reservedMemoryInMb) * 100d);
        }

        private static (long, long) GetUsedMemory() =>
            (Profiler.GetTotalReservedMemoryLong(), Profiler.GetTotalAllocatedMemoryLong());
    }
}
