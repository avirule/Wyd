#region

using System.ComponentModel;
using Controllers.State;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public abstract class OptionDisplayTextController : MonoBehaviour
    {
        protected TextMeshProUGUI _TextObject;
        protected string _Format;

        protected void Awake()
        {
            _TextObject = GetComponent<TextMeshProUGUI>();
            _Format = _TextObject.text;
        }

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
