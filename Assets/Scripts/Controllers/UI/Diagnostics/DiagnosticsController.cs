using System;
using UnityEngine;
using UnityEngine.UI;

namespace Controllers.UI.Diagnostics
{
    public class DiagnosticsController : MonoBehaviour
    {
        private bool _DiagnosticKeyPressed;

        public GameObject DiagnosticPanel;
        public Text VersionText;

        private void Start()
        {
            VersionText.text = Application.version;
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
        }
    }
}