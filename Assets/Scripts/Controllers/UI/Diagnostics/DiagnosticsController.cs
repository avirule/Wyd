#region

using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Diagnostics
{
    public class DiagnosticsController : MonoBehaviour
    {
        private bool _DiagnosticKeyPressed;

        public GameObject DiagnosticPanel;
        public TextMeshProUGUI VersionText;

        private void Start()
        {
        }

        private void Update()
        {
            CheckPressedDiagnosticKey();
        }

        private void CheckPressedDiagnosticKey()
        {
            if (!Input.GetKey(KeyCode.F3))
            {
                _DiagnosticKeyPressed = false;
                return;
            }

            if (_DiagnosticKeyPressed)
            {
                return;
            }

            _DiagnosticKeyPressed = true;
            DiagnosticPanel.SetActive(!DiagnosticPanel.activeSelf);
            VersionText.enabled = DiagnosticPanel.activeSelf;
        }
    }
}