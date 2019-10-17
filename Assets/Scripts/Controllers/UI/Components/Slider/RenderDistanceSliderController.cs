#region

using UnityEngine;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Slider
{
    public class RenderDistanceSliderController : MonoBehaviour
    {
        private UnityEngine.UI.Slider _RenderDistanceSlider;

        private void Awake()
        {
            _RenderDistanceSlider = GetComponent<UnityEngine.UI.Slider>();
            _RenderDistanceSlider.maxValue = OptionsController.MAXIMUM_RENDER_DISTANCE;
            _RenderDistanceSlider.onValueChanged.AddListener(RenderDistanceSliderValueChanged);
        }

        private void OnEnable()
        {
            UpdateSliderValue();
        }

        private static void RenderDistanceSliderValueChanged(float newValue)
        {
            OptionsController.Current.RenderDistance = (int)newValue;
        }

        private void UpdateSliderValue()
        {
            if (OptionsController.Current == null)
            {
                return;
            }

            _RenderDistanceSlider.value = OptionsController.Current.RenderDistance;
        }
    }
}
