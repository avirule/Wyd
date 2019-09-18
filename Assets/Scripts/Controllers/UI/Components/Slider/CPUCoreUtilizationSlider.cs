#region

using System;
using Controllers.State;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Slider
{
    public class CPUCoreUtilizationSlider : MonoBehaviour
    {
        private UnityEngine.UI.Slider _CPUCoreUtilizationSlider;

        private void Awake()
        {
            _CPUCoreUtilizationSlider = GetComponent<UnityEngine.UI.Slider>();
            _CPUCoreUtilizationSlider.onValueChanged.AddListener(OnSliderValueChanged);
            _CPUCoreUtilizationSlider.maxValue = Environment.ProcessorCount;
            _CPUCoreUtilizationSlider.minValue = 1;
        }

        private void OnEnable()
        {
            UpdateSliderValue();
        }

        private static void OnSliderValueChanged(float value)
        {
            OptionsController.Current.CPUCoreUtilization = (int) value;
        }

        private void UpdateSliderValue()
        {
            _CPUCoreUtilizationSlider.value = OptionsController.Current.CPUCoreUtilization;
        }
    }
}
