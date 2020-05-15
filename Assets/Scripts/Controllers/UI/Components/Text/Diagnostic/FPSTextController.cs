#region

using System;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class FPSTextController : FormattedTextController
    {
        private int _FramesWaited;
        private bool _Enabled;

        protected override void Awake()
        {
            base.Awake();

            _FramesWaited = 0;
        }

        private void OnEnable()
        {
            Singletons.Diagnostics.Instance.RegisterDiagnosticBuffer("FrameTimes");

            _Enabled = true;
        }

        private void OnDisable()
        {
            Singletons.Diagnostics.Instance.UnregisterDiagnosticTimeEntry("FrameTimes");

            _Enabled = false;
        }

        private void Update()
        {
            if (!_Enabled)
            {
                return;
            }

            TimeSpan delta = TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond * Time.deltaTime));
            Singletons.Diagnostics.Instance["FrameTimes"].Enqueue(delta);

            if (_FramesWaited < 4)
            {
                _FramesWaited += 1;
                return;
            }

            TimeSpan averageFPS = Singletons.Diagnostics.Instance.GetAverage("FrameTimes");
            double framesPerSecond = 1d / averageFPS.TotalSeconds;
            double framesInMilliseconds = averageFPS.TotalMilliseconds;

            _TextObject.text = string.Format(_Format, framesPerSecond, framesInMilliseconds);
            _FramesWaited = 0;
        }
    }
}
