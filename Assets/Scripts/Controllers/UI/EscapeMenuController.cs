#region

using Controllers.Game;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Controllers.UI
{
    public class EscapeMenuController : MonoBehaviour
    {
        private bool _EscapeKeyPressed;
        private bool _CursorUnlocked;

        public GameObject Backdrop;
        public GameObject Main;
        public GameObject Options;
        public Button OptionsButton;

        // Start is called before the first frame update
        private void Awake()
        {
            Backdrop.SetActive(false);
            Main.SetActive(false);
            Options.SetActive(false);
        }

        private void Start()
        {
            OptionsButton.onClick.AddListener(() => SetOptionsActive(true));
        }

        private void Update()
        {
            CheckPressedEscapeKey();

            if (_CursorUnlocked && !Main.activeSelf && !Options.activeSelf)
            {
                GameController.Current.ToggleCursorLocked(true);
            }
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
                SetOptionsActive(false);
                ToggleCursorUnlocked();
            }
            
            Backdrop.SetActive(active);
            Main.SetActive(active);
        }

        private void SetOptionsActive(bool active)
        {
            if (active)
            {
                SetMainActive(false);
                ToggleCursorUnlocked();
            }

            Backdrop.SetActive(active);
            Options.SetActive(active);
        }

        private void ToggleCursorUnlocked()
        {
            _CursorUnlocked = true;
            GameController.Current.ToggleCursorLocked(false);
        }
    }
}