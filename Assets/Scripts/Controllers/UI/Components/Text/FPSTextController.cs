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
        private const double _LOCAL_FRAME_INTERVAL = 1f / 15f;
        private Stopwatch _LocalFrameStopwatch;
        private List<double> _DeltaTimes;
        private TextMeshProUGUI _FPSText;

        private void Awake()
        {
            _LocalFrameStopwatch = new Stopwatch();
            _DeltaTimes = new List<double>();
            _FPSText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            _LocalFrameStopwatch.Start();
        }

        private void Update()
        {
            if (_LocalFrameStopwatch.Elapsed.TotalSeconds < WorldController.Current.WorldTickRate.TotalSeconds)
            {
                return;
            }

            _LocalFrameStopwatch.Restart();

            UpdateDeltaTimes();
        }

        private void LateUpdate()
        {
            if (_DeltaTimes.Count == 0)
            {
                return;
            }

            double averageDelaTime = Math.Round(_DeltaTimes.Average(), 4);

            _FPSText.text = $"FPS: {averageDelaTime}";
        }

        private void UpdateDeltaTimes()
        {
            _DeltaTimes.Add(1d / Time.deltaTime);

            if (_DeltaTimes.Count > OptionsController.Current.MaximumFrameRateBufferSize)
            {
                _DeltaTimes.RemoveRange(0,
                    _DeltaTimes.Count - OptionsController.Current.MaximumFrameRateBufferSize);
            }
        }
    }
}