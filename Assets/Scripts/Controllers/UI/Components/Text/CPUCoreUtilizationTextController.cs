#region

using System.ComponentModel;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class CPUCoreUtilizationTextController : OptionDisplayTextController
    {
        protected override void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
            if (force || args.PropertyName.Equals(nameof(OptionsController.Current.CPUCoreUtilization)))
            {
                TextObject.text = string.Format(Format, OptionsController.Current.CPUCoreUtilization);
            }
        }
    }
}
