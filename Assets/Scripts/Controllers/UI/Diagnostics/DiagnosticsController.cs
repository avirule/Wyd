#region

using Controllers.State;
using UnityEngine;

#endregion

namespace Controllers.UI.Diagnostics
{
    public class DiagnosticsController : MonoBehaviour
    {
        private bool _DiagnosticKeyPressed;

        public GameObject DiagnosticPanel;

        private void Awake()
        {
            DiagnosticPanel.SetActive(Debug.isDebugBuild);
        }

        private void Update()
        {
            CheckPressedDiagnosticKey();
        }

        private void CheckPressedDiagnosticKey()
        {
            if (!InputController.Current.GetKey(KeyCode.F3))
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
