using Static;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Controllers.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public Button ExitButton;
        public Button PlayButton;

        // Start is called before the first frame update
        private void Start()
        {
            ExitButton.onClick.AddListener(() => StaticMethods.ApplicationClose());
            PlayButton.onClick.AddListener(PlayGame);
        }

        // Update is called once per frame
        private void Update()
        {
        }

        private void PlayGame()
        {
            SceneManager.UnloadSceneAsync("MainMenu", UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
        }
    }
}