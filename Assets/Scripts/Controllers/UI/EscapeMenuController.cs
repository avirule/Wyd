#region

using Static;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#endregion

namespace Controllers.UI
{
    public class EscapeMenuController : MonoBehaviour
    {
        public Button QuitMainMenuButton;
        public Button ExitButton;

        // Start is called before the first frame update
        private void Awake()
        {
            QuitMainMenuButton.onClick.AddListener(QuitToMainMenu);
            ExitButton.onClick.AddListener(() => StaticMethods.ApplicationClose());
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }

        private static void QuitToMainMenu()
        {
            SceneManager.UnloadSceneAsync("Game");
            SceneManager.LoadSceneAsync("MainMenu");
        }
    }
}