#region

using System;
using System.Diagnostics;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class UpdatingFormattedTextController : FormattedTextController
    {
        private float _UpdatesPerSecond;
        private TimeSpan _UpdatesPerSecondTimeSpan;
        private Stopwatch _UpdateTimer;

        protected float UpdatesPerSecond
        {
            get => _UpdatesPerSecond;
            set
            {
                _UpdatesPerSecond = value;
                _UpdatesPerSecondTimeSpan = TimeSpan.FromSeconds(1d / _UpdatesPerSecond);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            UpdatesPerSecond = 4f;
            _UpdateTimer = Stopwatch.StartNew();
        }

        protected virtual void Update()
        {
            if (_UpdateTimer.Elapsed < _UpdatesPerSecondTimeSpan)
            {
                return;
            }

            _UpdateTimer.Restart();

            TimedUpdate();
        }

        protected virtual void TimedUpdate() { }
    }
}
