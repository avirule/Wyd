#region

using Controllers.State;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#endregion

namespace Controllers.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private bool _EscapeKeyPressed;

        public Button PlayButton;
        public Button OptionsButton;

        public GameObject MainMenu;
        public GameObject OptionsMenu;

        private void Awake()
        {
            OptionsMenu.SetActive(false);
        }

        // Start is called before the first frame update
        private void Start()
        {
            OptionsButton.onClick.AddListener(() => SetOptionsMenuActive(true));
            PlayButton.onClick.AddListener(PlayGame);
            InputController.Current.ToggleCursorLocked(false);
        }

        private void Update()
        {
            CheckPressedEscapeKey();
        }

        public void PlayGame()
        {
            SceneManager.LoadSceneAsync("Scenes/Game", LoadSceneMode.Single);
        }

        private void CheckPressedEscapeKey()
        {
            if (!InputController.Current.GetKey(KeyCode.Escape, this))
            {
                _EscapeKeyPressed = false;
                return;
            }

            if (_EscapeKeyPressed || MainMenu.activeSelf)
            {
                return;
            }

            _EscapeKeyPressed = true;
            SetMainActive(!MainMenu.activeSelf);
        }

        private void SetMainActive(bool active)
        {
            MainMenu.SetActive(active);

            if (active)
            {
                SetOptionsMenuActive(false);
            }
        }

        private void SetOptionsMenuActive(bool active)
        {
            OptionsMenu.SetActive(active);

            if (active)
            {
                SetMainActive(false);
            }
        }
    }
}
