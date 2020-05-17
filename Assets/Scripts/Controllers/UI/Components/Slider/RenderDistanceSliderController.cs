#region

using UnityEngine;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Slider
{
    public class RenderDistanceSliderController : MonoBehaviour
    {
        private UnityEngine.UI.Slider _RenderDistanceSlider;

        private void Awake()
        {
            _RenderDistanceSlider = GetComponent<UnityEngine.UI.Slider>();
            _RenderDistanceSlider.maxValue = 16;
            _RenderDistanceSlider.onValueChanged.AddListener(RenderDistanceSliderValueChanged);
        }

        private void OnEnable()
        {
            UpdateSliderValue();
        }

        private static void RenderDistanceSliderValueChanged(float newValue)
        {
            Options.Instance.RenderDistance = (int)newValue;
        }

        private void UpdateSliderValue()
        {
            _RenderDistanceSlider.value = Options.Instance.RenderDistance;
        }
    }
}
