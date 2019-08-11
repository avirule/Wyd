#region

using Controllers.Game;
using UnityEngine;

#endregion

namespace Controllers.UI
{
    public class UIInputWatcherController : MonoBehaviour
    {
        private bool _DiagnosticKeyPressed;
        private bool _EscapeKeyPressed;

        public GameObject DiagnosticMenu;
        public GameObject EscapeMenu;

        // Update is called once per frame
        private void Update()
        {
            CheckPresses();
        }

        private void CheckPresses()
        {
            CheckPressedDiagnosticKey();
            CheckPressedEscapeKey();
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
            DiagnosticMenu.SetActive(!DiagnosticMenu.activeSelf);
        }

        private void CheckPressedEscapeKey()
        {
            if (!Input.GetKey(KeyCode.Escape))
            {
                _EscapeKeyPressed = false;
                return;
            }

            if (_EscapeKeyPressed)
            {
                return;
            }

            _EscapeKeyPressed = true;
            EscapeMenu.SetActive(!EscapeMenu.activeSelf);
            GameController.ToggleCursorLocked(!EscapeMenu.activeSelf);
        }
    }
}