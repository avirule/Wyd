#region

using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class VSyncLevelTextController : MonoBehaviour
    {
        private string _Format;
        private TextMeshProUGUI _VSyncLevelText;
        private int _LastVSyncCount;

        private void Awake()
        {
            _VSyncLevelText = GetComponent<TextMeshProUGUI>();
            _Format = _VSyncLevelText.text;
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

            _VSyncLevelText.text = string.Format(_Format, _LastVSyncCount);
        }
    }
}
