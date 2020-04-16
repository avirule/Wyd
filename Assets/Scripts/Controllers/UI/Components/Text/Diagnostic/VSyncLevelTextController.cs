#region

using System.ComponentModel;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class VSyncLevelTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.VSyncLevel)))
            {
                _TextObject.text = string.Format(_Format,
                    OptionsController.Current.VSyncLevel == 1 ? "Enabled" : "Disabled");
            }
        }
    }
}
