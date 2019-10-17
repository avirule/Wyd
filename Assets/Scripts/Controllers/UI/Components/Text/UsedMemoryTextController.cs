#region

using UnityEngine.Profiling;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class UsedMemoryTextController : UpdatingFormattedTextController
    {
        private const float _MEGABYTE_VALUE = 1_000_000f;

        private long _ReservedMemory;
        private long _AllocatedMemory;
        private long _HeapMemory;

        protected override void TimedUpdate()
        {
            (long reservedMemory, long allocatedMemory, long heapMemory) = GetUsedMemory();

            if ((reservedMemory != _ReservedMemory)
                || (allocatedMemory != _AllocatedMemory)
                || (heapMemory != _HeapMemory))
            {
                UpdateUsedMemoryText(reservedMemory, allocatedMemory, heapMemory);
            }
        }

        private void UpdateUsedMemoryText(long reservedMemory, long allocatedMemory, long heapMemory)
        {
            _ReservedMemory = reservedMemory;
            _AllocatedMemory = allocatedMemory;
            _HeapMemory = heapMemory;

            TextObject.text = string.Format(Format,
                _ReservedMemory / _MEGABYTE_VALUE,
                _AllocatedMemory / _MEGABYTE_VALUE,
                _HeapMemory / _MEGABYTE_VALUE);
        }

        private static (long, long, long) GetUsedMemory() =>
            (Profiler.GetTotalReservedMemoryLong(), Profiler.GetTotalAllocatedMemoryLong(), Profiler.usedHeapSizeLong);
    }
}
