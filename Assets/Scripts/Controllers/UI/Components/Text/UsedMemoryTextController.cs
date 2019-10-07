#region

using System;
using System.Diagnostics;
using Tayx.Graphy;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class UsedMemoryTextController : FormattedTextController
    {
        [SerializeField]
        private int UpdatesPerSecond = 4;

        private TimeSpan _UpdatesPerSecondTimeSpan;
        private Stopwatch _UpdateTimer;
        private float _ReservedMemory;
        private float _AllocatedMemory;
        private float _MonoMemory;

        protected override void Awake()
        {
            base.Awake();

            _UpdatesPerSecondTimeSpan = TimeSpan.FromSeconds(1d / UpdatesPerSecond);
            _UpdateTimer = Stopwatch.StartNew();
        }

        private void Update()
        {
            if (_UpdateTimer.Elapsed <= _UpdatesPerSecondTimeSpan)
            {
                return;
            }

            _UpdateTimer.Restart();

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
                (_AllocatedMemory / (double) _ReservedMemory) * 100d,
                _MonoMemory = monoMemory);
        }

        private static (float, float, float) GetUsedMemory() =>
            (GraphyManager.Instance.ReservedRam, GraphyManager.Instance.AllocatedRam, GraphyManager.Instance.MonoRam);
    }
}
