#region

using System.ComponentModel;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class ShadowDistanceTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.ShadowDistance)))
            {
                _TextObject.text = string.Format(_Format, OptionsController.Current.ShadowDistance);
            }
        }
    }
}
