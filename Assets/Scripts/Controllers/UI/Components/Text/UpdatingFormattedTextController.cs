#region

using System;
using System.Diagnostics;
using Wyd.Controllers.System;
using Wyd.System;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class UpdatingFormattedTextController : FormattedTextController, IPerFrameUpdate
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
                _UpdatesPerSecondTimeSpan = TimeSpan.FromSeconds(1d / UpdatesPerSecond);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            UpdatesPerSecond = 1f;
            _UpdateTimer = Stopwatch.StartNew();
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(150, this);
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(150, this);
        }

        public virtual void FrameUpdate()
        {
            if ((_UpdateTimer == null) || (_UpdateTimer.Elapsed < _UpdatesPerSecondTimeSpan))
            {
                return;
            }

            _UpdateTimer.Restart();

            TimedUpdate();
        }

        protected virtual void TimedUpdate() { }
    }
}
