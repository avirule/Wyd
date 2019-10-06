#region

using UnityEngine;
using UnityEngine.UI;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI
{
    public class EscapeMenuController : MonoBehaviour
    {
        private bool _EscapeKeyPressed;
        private bool _CursorUnlocked;

        public GameObject Backdrop;
        public GameObject EscapeMenu;
        public GameObject OptionsMenu;
        public Button OptionsButton;

        // Start is called before the first frame update
        private void Awake()
        {
            Backdrop.SetActive(false);
            EscapeMenu.SetActive(false);
            OptionsMenu.SetActive(false);
        }

        private void Start()
        {
            OptionsButton.onClick.AddListener(() => SetOptionsMenuActive(true));
        }

        private void Update()
        {
            CheckPressedEscapeKey();
        }

        private void OnDisable()
        {
            if (InputController.Current == default)
            {
                return;
            }

            InputController.Current.Unlock(this);
        }

        private void OnDestroy()
        {
            if (InputController.Current == default)
            {
                return;
            }

            InputController.Current.Unlock(this);
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
            SetMainActive(!EscapeMenu.activeSelf);
        }

        private void SetMainActive(bool active)
        {
            Backdrop.SetActive(active);
            EscapeMenu.SetActive(active);

            if (active && InputController.Current.Lock(this))
            {
                InputController.Current.ToggleCursorLocked(false, this);
                OptionsMenu.SetActive(false);
            }
            else if (!active && !OptionsMenu.activeSelf)
            {
                // this state should be effectively reached when the main menu is being exited
                InputController.Current.ToggleCursorLocked(true, this);
                InputController.Current.Unlock(this);
            }
        }

        private void SetOptionsMenuActive(bool active)
        {
            Backdrop.SetActive(active);
            OptionsMenu.SetActive(active);

            if (active)
            {
                EscapeMenu.SetActive(false);
            }
        }
    }
}
