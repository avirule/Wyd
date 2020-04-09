#region

using UnityEngine;
using Wyd.Controllers.System;
using Wyd.System;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class PerFrameUpdaterStatusTextController : FormattedTextController, IPerFrameUpdate
    {
        private const int _STALL_THRESHOLD = 10;

        private int _UnityUpdateCounter;
        private bool _TextNotifiesStalled;

        protected override void Awake()
        {
            base.Awake();

            UpdateText(false);
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(int.MaxValue, this);
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(int.MaxValue, this);
        }

        private void Update()
        {
            if ((_UnityUpdateCounter >= _STALL_THRESHOLD) && !_TextNotifiesStalled)
            {
                UpdateText(true);
            }
            else if ((_UnityUpdateCounter == 0) && _TextNotifiesStalled)
            {
                UpdateText(false);
            }

            _UnityUpdateCounter += 1;
        }

        public void FrameUpdate()
        {
            if (PerFrameUpdateController.Current.IsSafeFrameTime())
            {
                _UnityUpdateCounter = 0;
            }
        }

        private void UpdateText(bool stalled)
        {
            if (stalled)
            {
                TextObject.text = "STALLING";
                TextObject.color = Color.red;
                _TextNotifiesStalled = true;
            }
            else
            {
                TextObject.text = "NORMAL";
                TextObject.color = Color.green;
                _TextNotifiesStalled = false;
            }
        }
    }
}
