#region

using Controllers.Game;
using UnityEngine;

#endregion

namespace Controllers.UI
{
    public class EscapeMenuController : MonoBehaviour
    {
        private bool _EscapeKeyPressed;

        public GameObject Main;
        public GameObject Options;

        // Start is called before the first frame update
        private void Awake()
        {
            Main.SetActive(false);
            Options.SetActive(false);
        }

        private void Update()
        {
            CheckPressedEscapeKey();
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
            SetMainActive(!Main.activeSelf);
        }

        private void SetMainActive(bool active)
        {
            if (active)
            {
                Options.SetActive(false);
            }

            Main.SetActive(active);
            GameController.Current.ToggleCursorLocked(active);
        }
    }
}