#region

using Controllers.State;
using Jobs;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ThreadingModeTextController : MonoBehaviour
    {
        private string _Format;
        private TextMeshProUGUI _ThreadingModeText;
        private ThreadingMode _LastThreadingMode;

        private void Awake()
        {
            _ThreadingModeText = GetComponent<TextMeshProUGUI>();
            _Format = _ThreadingModeText.text;
        }

        private void Update()
        {
            if (OptionsController.Current.ThreadingMode != _LastThreadingMode)
            {
                UpdateThreadingModeText();
            }
        }

        private void UpdateThreadingModeText()
        {
            _LastThreadingMode = OptionsController.Current.ThreadingMode;

            _ThreadingModeText.text = string.Format(_Format, _LastThreadingMode.ToString());
        }
    }
}
