#region

using System;
using System.Diagnostics;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class UpdatingFormattedTextController : FormattedTextController
    {
        [SerializeField]
        private int UpdatesPerSecond = 4;

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
            if (_UpdateTimer.Elapsed < _UpdatesPerSecondTimeSpan)
            {
                return;
            }

            _UpdateTimer.Restart();

            TimedUpdate();
        }

        protected virtual void TimedUpdate()
        {
        }
    }
}
