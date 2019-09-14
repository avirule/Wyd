#region

using Controllers.State;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class GPUAccelerationTextController : MonoBehaviour
    {
        private readonly string _FormatString = "GPU Acceleration {0}";

        private TextMeshProUGUI _GPUAccelerationText;
        private bool _LastGPUAccelerationSetting;

        private void Awake()
        {
            _GPUAccelerationText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            UpdateGPUAcceleration();
        }

        private void Update()
        {
            if (OptionsController.Current.GPUAcceleration != _LastGPUAccelerationSetting)
            {
                UpdateGPUAcceleration();
            }
        }

        private void UpdateGPUAcceleration()
        {
            _GPUAccelerationText.text = string.Format(_FormatString, GetGPUAccelerationAsEnabledStatus());
            _LastGPUAccelerationSetting = OptionsController.Current.GPUAcceleration;
        }

        private static string GetGPUAccelerationAsEnabledStatus()
        {
            return OptionsController.Current.GPUAcceleration ? "Enabled" : "Disabled";
        }
    }
}
