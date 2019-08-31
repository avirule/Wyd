#region

using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class VSyncLevelTextController : MonoBehaviour
    {
        private TextMeshProUGUI _VSyncLevelText;
        private int _LastVSyncCount;

        private void Awake()
        {
            _VSyncLevelText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            UpdateVSyncLevelText();
        }

        private void Update()
        {
            if (_LastVSyncCount != QualitySettings.vSyncCount)
            {
                UpdateVSyncLevelText();
            }
        }

        private void UpdateVSyncLevelText()
        {
            _LastVSyncCount = QualitySettings.vSyncCount;

            _VSyncLevelText.text = $"VSync Level: {_LastVSyncCount}";
        }
    }
}