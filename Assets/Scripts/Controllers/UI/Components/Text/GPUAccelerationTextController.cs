#region

using System.ComponentModel;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class GPUAccelerationTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(Options.Instance.GPUAcceleration)))
            {
                _TextObject.text = string.Format(_Format, Options.Instance.GPUAcceleration ? "Enabled" : "Disabled");
            }
        }
    }
}
