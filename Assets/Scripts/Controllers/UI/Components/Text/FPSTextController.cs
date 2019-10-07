#region

using System;
using System.Diagnostics;
using Tayx.Graphy;
using UnityEngine;

// ReSharper disable InconsistentNaming

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class FPSTextController : FormattedTextController
    {
        [SerializeField]
        private int UpdatesPerSecond = 5;

        private TimeSpan _UpdatesPerSecondTimeSpan;
        private Stopwatch _UpdateTimer;

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

            float averageFPS = GraphyManager.Instance.AverageFPS;
            float averageFrameTimeMilliseconds = 1f / averageFPS;

            TextObject.text = string.Format(Format, averageFPS, averageFrameTimeMilliseconds);
        }
    }
}
