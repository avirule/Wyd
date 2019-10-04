#region

using System.ComponentModel;
using Controllers.State;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ThreadingModeTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.ThreadingMode)))
            {
                TextObject.text = string.Format(Format, OptionsController.Current.ThreadingMode);
            }
        }
    }
}
