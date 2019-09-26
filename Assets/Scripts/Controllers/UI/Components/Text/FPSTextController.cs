#region

using System.Collections.Generic;
using System.Linq;
using Controllers.State;
using UnityEngine;

// ReSharper disable InconsistentNaming

#endregion

namespace Controllers.UI.Components.Text
{
    public class FPSTextController : FormattedTextController
    {
        private List<float> _DeltaTimes;
        private int _SkippedFrames;

        public int SkipFrames = 4;

        protected override void Awake()
        {
            base.Awake();

            _DeltaTimes = new List<float>();
            _SkippedFrames = 0;

            // avoids div by zero
            if (SkipFrames <= 0)
            {
                SkipFrames = 1;
            }
        }

        private void Update()
        {
            UpdateDeltaTimes();
        }

        private void LateUpdate()
        {
            if ((_DeltaTimes.Count == 0) || (_SkippedFrames < SkipFrames))
            {
                _SkippedFrames++;
                return;
            }

            double averageDeltaTime = _DeltaTimes.Average();
            double averageDeltaTimeAsFrames = 1d / averageDeltaTime;
            double averageDeltaTimeAsMillisecondsRounded = 1000d * averageDeltaTime;

            _TextObject.text = string.Format(_Format, averageDeltaTimeAsFrames, averageDeltaTimeAsMillisecondsRounded);
            _SkippedFrames = 0;
        }

        private void UpdateDeltaTimes()
        {
            _DeltaTimes.Add(Time.deltaTime);

            if (_DeltaTimes.Count > OptionsController.Current.MaximumFrameRateBufferSize)
            {
                _DeltaTimes.RemoveRange(0,
                    _DeltaTimes.Count - OptionsController.Current.MaximumFrameRateBufferSize);
            }
        }
    }
}
