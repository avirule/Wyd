#region

using System.ComponentModel;
using Wyd.Controllers.State;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class VSyncLevelTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(Options.Instance.VSync)))
            {
                _TextObject.text = string.Format(_Format, Options.Instance.VSync ? "Enabled" : "Disabled");
            }
        }
    }
}
