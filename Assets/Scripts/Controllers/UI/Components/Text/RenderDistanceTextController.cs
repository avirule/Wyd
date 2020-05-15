#region

using System.ComponentModel;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class RenderDistanceTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(Options.Instance.RenderDistance)))
            {
                _TextObject.text = string.Format(_Format, Options.Instance.RenderDistance);
            }
        }
    }
}
