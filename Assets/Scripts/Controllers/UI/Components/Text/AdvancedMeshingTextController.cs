#region

using System.ComponentModel;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class AdvancedMeshingTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(Options.Instance.AdvancedMeshing)))
            {
                _TextObject.text = string.Format(_Format, Options.Instance.AdvancedMeshing ? "Enabled" : "Disabled");
            }
        }
    }
}
