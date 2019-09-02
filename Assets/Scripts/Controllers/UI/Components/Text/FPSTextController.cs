#region

using System;
using System.Collections.Generic;
using System.Linq;
using Controllers.Game;
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

        public int MinimumSkipFrames = 8;
        public int Precision = 2;

        private void Awake()
        {
            _DeltaTimes = new List<float>();
            _FPSText = GetComponent<TextMeshProUGUI>();
            _SkippedFrames = 0;
        }

        private void Update()
        {
            // avoids div by zero
            if (MinimumSkipFrames <= 0)
            {
                MinimumSkipFrames = 1;
            }

            UpdateDeltaTimes();
        }

        private void LateUpdate()
        {
            if ((_DeltaTimes.Count == 0) ||
                ((_SkippedFrames % MinimumSkipFrames) != 0))
            {
                return;
            }

            double averageDelaTime = Math.Round(1f / _DeltaTimes.Average(), Precision);

            _FPSText.text = $"FPS: {averageDelaTime}";
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