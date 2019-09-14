#region

using Controllers.State;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class GPUAccelerationTextController : MonoBehaviour
    {
        private string _Format;
        private TextMeshProUGUI _GPUAccelerationText;
        private bool _LastGPUAccelerationSetting;

        private void Awake()
        {
            _GPUAccelerationText = GetComponent<TextMeshProUGUI>();
            _Format = _GPUAccelerationText.text;
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
            _GPUAccelerationText.text = string.Format(_Format, GetGPUAccelerationAsEnabledStatus());
            _LastGPUAccelerationSetting = OptionsController.Current.GPUAcceleration;
        }

        private static string GetGPUAccelerationAsEnabledStatus()
        {
            return OptionsController.Current.GPUAcceleration ? "Enabled" : "Disabled";
        }
    }
}
