#region

using System.ComponentModel;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class VSyncLevelTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.VSyncLevel)))
            {
                TextObject.text = string.Format(Format,
                    OptionsController.Current.VSyncLevel == 1 ? "Enabled" : "Disabled");
            }
        }
    }
}
