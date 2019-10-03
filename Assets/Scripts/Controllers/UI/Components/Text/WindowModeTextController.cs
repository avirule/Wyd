#region

using System.ComponentModel;
using Controllers.State;
using Graphics;

#endregion

namespace Controllers.UI.Components.Text
{
    public class WindowModeTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.WindowMode)))
            {
                _TextObject.text = OptionsController.Current.WindowMode == WindowMode.BorderlessWindowed
                    ? string.Format(_Format, "Borderless Windowed")
                    : string.Format(_Format, OptionsController.Current.WindowMode);
            }
        }
    }
}
