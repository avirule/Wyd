#region

using System.Linq;
using UnityEngine;
using Wyd.System.Collections;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class FPSTextController : UpdatingFormattedTextController
    {
        private const int _ACCURACY = 300;

        private FixedConcurrentQueue<float> _FrameTimes;

        protected override void Awake()
        {
            base.Awake();

            _FrameTimes = new FixedConcurrentQueue<float>(_ACCURACY);
        }

        protected override void Update()
        {
            _FrameTimes.Enqueue(Time.deltaTime);

            base.Update();
        }

        protected override void TimedUpdate()
        {
            float averageFPS = 1f / _FrameTimes.Average();
            float averageFrameTimeMilliseconds = (1f / averageFPS) * 1000f;

            TextObject.text = string.Format(Format, averageFPS, averageFrameTimeMilliseconds);
        }
    }
}
