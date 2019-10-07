#region

using System;
using Tayx.Graphy;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class UsedMemoryTextController : UpdatingFormattedTextController
    {
        private float _ReservedMemory;
        private float _AllocatedMemory;
        private float _MonoMemory;

        protected override void TimedUpdate()
        {
            (float reservedMemory, float allocatedMemory, float monoMemory) = GetUsedMemory();

            if ((Math.Abs(reservedMemory - _ReservedMemory) > 0.009)
                || (Math.Abs(allocatedMemory - _AllocatedMemory) > 0.009)
                || (Math.Abs(monoMemory - _MonoMemory) > 0.009))
            {
                UpdateUsedMemoryText(reservedMemory, allocatedMemory, monoMemory);
            }
        }

        private void UpdateUsedMemoryText(float reservedMemory, float allocatedMemory, float monoMemory)
        {
            TextObject.text = string.Format(Format,
                _ReservedMemory = reservedMemory,
                _AllocatedMemory = allocatedMemory,
                _MonoMemory = monoMemory);
        }

        private static (float, float, float) GetUsedMemory() =>
            (GraphyManager.Instance.ReservedRam, GraphyManager.Instance.AllocatedRam, GraphyManager.Instance.MonoRam);
    }
}
