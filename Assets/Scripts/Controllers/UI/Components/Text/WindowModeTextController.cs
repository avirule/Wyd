#region

using System.ComponentModel;
using Wyd.Controllers.State;
using Wyd.Graphics;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class WindowModeTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.WindowMode)))
            {
                TextObject.text = OptionsController.Current.WindowMode == WindowMode.BorderlessWindowed
                    ? string.Format(Format, "Borderless Windowed")
                    : string.Format(Format, OptionsController.Current.WindowMode);
            }
        }
    }
}
