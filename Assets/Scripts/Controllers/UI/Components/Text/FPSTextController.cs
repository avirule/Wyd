#region

using Tayx.Graphy;

// ReSharper disable InconsistentNaming

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class FPSTextController : UpdatingFormattedTextController
    {
        protected override void TimedUpdate()
        {
            float averageFPS = GraphyManager.Instance.AverageFPS;
            float averageFrameTimeMilliseconds = (1f / averageFPS) * 1000f;

            TextObject.text = string.Format(Format, averageFPS, averageFrameTimeMilliseconds);
        }
    }
}
