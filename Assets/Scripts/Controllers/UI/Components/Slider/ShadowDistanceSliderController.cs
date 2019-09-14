#region

using Controllers.State;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Slider
{
    public class ShadowDistanceSliderController : MonoBehaviour
    {
        private UnityEngine.UI.Slider _ShadowDistanceSlider;
        private int _LastShadowDistanceValueChecked;

        private void Awake()
        {
            _ShadowDistanceSlider = GetComponent<UnityEngine.UI.Slider>();
            _ShadowDistanceSlider.onValueChanged.AddListener(ShadowDistanceSliderValueChanged);
        }

        private void Start()
        {
            UpdateSliderValue();
        }

        private void Update()
        {
            if (_LastShadowDistanceValueChecked != OptionsController.Current.ShadowDistance)
            {
                UpdateSliderValue();
            }
        }

        private static void ShadowDistanceSliderValueChanged(float newValue)
        {
            OptionsController.Current.ShadowDistance = (int) newValue;
        }

        private void UpdateSliderValue()
        {
            _LastShadowDistanceValueChecked = OptionsController.Current.ShadowDistance;

            _ShadowDistanceSlider.value = _LastShadowDistanceValueChecked;
        }
    }
}
