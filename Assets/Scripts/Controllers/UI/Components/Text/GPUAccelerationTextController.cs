#region

using System.ComponentModel;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class GPUAccelerationTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.GPUAcceleration)))
            {
                _TextObject.text = string.Format(_Format,
                    OptionsController.Current.GPUAcceleration ? "Enabled" : "Disabled");
            }
        }
    }
}
