#region

using System.ComponentModel;
using Controllers.State;

#endregion

namespace Controllers.UI.Components.Text
{
    public abstract class OptionDisplayTextController : FormattedTextController
    {
        protected void Start()
        {
            OptionsController.Current.PropertyChanged += OnOptionControllerChangedProperty;
            UpdateTextObjectText(null, true);
        }

        private void OnOptionControllerChangedProperty(object sender, PropertyChangedEventArgs args)
        {
            UpdateTextObjectText(args);
        }

        protected virtual void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false)
        {
        }
    }
}
