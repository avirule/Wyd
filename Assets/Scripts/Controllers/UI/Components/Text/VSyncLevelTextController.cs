#region

using System.ComponentModel;
using Controllers.State;

#endregion

namespace Controllers.UI.Components.Text
{
    public class VSyncLevelTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.VSyncLevel)))
            {
                TextObject.text = string.Format(Format, OptionsController.Current.VSyncLevel);
            }
        }
    }
}
