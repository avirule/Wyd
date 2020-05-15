#region

using System.ComponentModel;
using Wyd.Controllers.State;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public abstract class OptionDisplayTextController : FormattedTextController
    {
        protected void Start()
        {
            Options.Instance.PropertyChanged += OnOptionControllerChangedProperty;
            UpdateTextObjectText(null, true);
        }

        private void OnOptionControllerChangedProperty(object sender, PropertyChangedEventArgs args)
        {
            UpdateTextObjectText(args);
        }

        protected abstract void UpdateTextObjectText(PropertyChangedEventArgs args, bool force = false);
    }
}
