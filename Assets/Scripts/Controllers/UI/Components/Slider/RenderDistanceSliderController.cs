#region

using Controllers.State;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Slider
{
    public class RenderDistanceSliderController : MonoBehaviour
    {
        private UnityEngine.UI.Slider _RenderDistanceSlider;
        private int _LastRenderDistanceValueChecked;

        private void Awake()
        {
            _RenderDistanceSlider = GetComponent<UnityEngine.UI.Slider>();
            _RenderDistanceSlider.onValueChanged.AddListener(RenderDistanceSliderValueChanged);
        }

        private void Start()
        {
            UpdateSliderValue();
        }

        private void Update()
        {
            if (_LastRenderDistanceValueChecked != OptionsController.Current.RenderDistance)
            {
                UpdateSliderValue();
            }
        }

        private static void RenderDistanceSliderValueChanged(float newValue)
        {
            OptionsController.Current.RenderDistance = (int) newValue;
        }

        private void UpdateSliderValue()
        {
            _LastRenderDistanceValueChecked = OptionsController.Current.RenderDistance;

            _RenderDistanceSlider.value = _LastRenderDistanceValueChecked;
        }
    }
}
