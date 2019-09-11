#region

using Controllers.State;
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
        }

        private void CheckPressedEscapeKey()
        {
            if (!InputController.Current.GetKey(KeyCode.Escape, this))
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
            if (active && InputController.Current.Lock(this))
            {
                InputController.Current.ToggleCursorLocked(false, this);
                SetOptionsActive(false);
            }
            else if (!active && !Options.activeSelf)
            {
                // this state should be effectively reached when the main menu is being exited
                InputController.Current.ToggleCursorLocked(true, this);
                InputController.Current.Unlock(this);
            }

            Backdrop.SetActive(active);
            Main.SetActive(active);
        }

        private void SetOptionsActive(bool active)
        {
            if (active)
            {
                SetMainActive(false);
            }

            Backdrop.SetActive(active);
            Options.SetActive(active);
        }
    }
}
