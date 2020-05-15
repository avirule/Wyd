#region

using System.ComponentModel;
using Wyd.Extensions;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class FullScreenModeTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(Options.Instance.FullScreenMode)))
            {
                _TextObject.text = string.Format(_Format, Options.Instance.FullScreenMode.GetAlias());
            }
        }
    }
}
