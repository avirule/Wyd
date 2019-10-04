#region

using Controllers.State;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Slider
{
    public class ShadowDistanceSliderController : MonoBehaviour
    {
        private UnityEngine.UI.Slider _ShadowDistanceSlider;

        private void Awake()
        {
            _ShadowDistanceSlider = GetComponent<UnityEngine.UI.Slider>();
            _ShadowDistanceSlider.maxValue = OptionsController.MAXIMUM_RENDER_DISTANCE;
            _ShadowDistanceSlider.onValueChanged.AddListener(ShadowDistanceSliderValueChanged);
        }

        private void OnEnable()
        {
            UpdateSliderValue();
        }

        private static void ShadowDistanceSliderValueChanged(float newValue)
        {
            OptionsController.Current.ShadowDistance = (int) newValue;
        }

        private void UpdateSliderValue()
        {
            if (OptionsController.Current == null)
            {
                return;
            }

            _ShadowDistanceSlider.value = OptionsController.Current.ShadowDistance;
        }
    }
}
