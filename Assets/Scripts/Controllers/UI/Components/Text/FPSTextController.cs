#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Game;
using Controllers.World;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class FPSTextController : MonoBehaviour
    {
        private Stopwatch _LocalFrameStopwatch;
        private List<float> _DeltaTimes;
        private TextMeshProUGUI _FPSText;

        private void Awake()
        {
            _LocalFrameStopwatch = new Stopwatch();
            _DeltaTimes = new List<float>();
            _FPSText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            _LocalFrameStopwatch.Start();
        }

        private void Update()
        {
            _LocalFrameStopwatch.Restart();

            UpdateDeltaTimes();
        }

        private void LateUpdate()
        {
            if (_DeltaTimes.Count == 0)
            {
                return;
            }

            double averageDelaTime = Math.Round(1f / _DeltaTimes.Average(), 4);

            _FPSText.text = $"FPS: {averageDelaTime}";
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