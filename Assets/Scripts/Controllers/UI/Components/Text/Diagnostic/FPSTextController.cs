#region

using System.Linq;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.System.Collections;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class FPSTextController : FormattedTextController
    {
        private int _FramesWaited;

        private FixedConcurrentQueue<float> _FrameTimes;

        protected override void Awake()
        {
            base.Awake();

            _FramesWaited = 0;
        }

        private void Start()
        {
            _FrameTimes = new FixedConcurrentQueue<float>(OptionsController.Current.MaximumDiagnosticBuffersSize);
        }

        private void Update()
        {
            _FrameTimes.Enqueue(Time.deltaTime);

            if (_FramesWaited < 4)
            {
                _FramesWaited += 1;
                return;
            }


            float averageFPS = 1f / _FrameTimes.Average();
            float averageFrameTimeMilliseconds = (1f / averageFPS) * 1000f;

            TextObject.text = string.Format(Format, averageFPS, averageFrameTimeMilliseconds);
            _FramesWaited = 0;
        }
    }
}
