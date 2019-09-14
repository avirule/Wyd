#region

using Controllers.State;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ShadowDistanceTextController : MonoBehaviour
    {
        private string _Format;
        private TextMeshProUGUI _ShadowDistanceText;
        private int _LastShadowDistanceValueChecked;

        private void Awake()
        {
            _ShadowDistanceText = GetComponent<TextMeshProUGUI>();
            _Format = _ShadowDistanceText.text;
        }

        private void Start()
        {
            UpdateShadowDistanceText();
        }

        private void Update()
        {
            if (_LastShadowDistanceValueChecked != OptionsController.Current.ShadowDistance)
            {
                UpdateShadowDistanceText();
            }
        }

        private void UpdateShadowDistanceText()
        {
            _LastShadowDistanceValueChecked = OptionsController.Current.ShadowDistance;

            _ShadowDistanceText.text = string.Format(_Format, _LastShadowDistanceValueChecked);
        }
    }
}
