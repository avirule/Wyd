#region

using System.ComponentModel;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class RenderDistanceTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.RenderDistance)))
            {
                _TextObject.text = string.Format(_Format, OptionsController.Current.RenderDistance);
            }
        }
    }
}
