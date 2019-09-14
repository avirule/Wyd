#region

using Controllers.State;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class RenderDistanceTextController : MonoBehaviour
    {
        private string _Format;
        private TextMeshProUGUI _RenderDistanceText;
        private int _LastRenderDistanceValueChecked;

        private void Awake()
        {
            _RenderDistanceText = GetComponent<TextMeshProUGUI>();
            _Format = _RenderDistanceText.text;
        }

        private void Start()
        {
            UpdateShadowDistanceText();
        }

        private void Update()
        {
            if (_LastRenderDistanceValueChecked != OptionsController.Current.RenderDistance)
            {
                UpdateShadowDistanceText();
            }
        }

        private void UpdateShadowDistanceText()
        {
            _LastRenderDistanceValueChecked = OptionsController.Current.RenderDistance;

            _RenderDistanceText.text = string.Format(_Format, _LastRenderDistanceValueChecked);
        }
    }
}
