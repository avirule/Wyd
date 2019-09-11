#region

using System.Collections.Generic;
using System.Linq;
using Controllers.State;
using TMPro;
using UnityEngine;

// ReSharper disable InconsistentNaming

#endregion

namespace Controllers.UI.Components.Text
{
    public class FPSTextController : MonoBehaviour
    {
        private List<float> _DeltaTimes;
        private TextMeshProUGUI _FPSText;
        private int _SkippedFrames;

        public int SkipFrames = 4;

        private void Awake()
        {
            _DeltaTimes = new List<float>();
            _FPSText = GetComponent<TextMeshProUGUI>();
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

            _FPSText.text = $"({averageDeltaTimeAsFrames:0}fps, {averageDeltaTimeAsMillisecondsRounded:0.00}ms)";
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
