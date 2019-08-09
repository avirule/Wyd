using Static;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Controllers.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public Button PlayButton;
        public Button ExitButton;
        
        // Start is called before the first frame update
        private void Start()
        {
            ExitButton.onClick.AddListener(() => StaticMethods.ApplicationClose());
            PlayButton.onClick.AddListener(PlayGame);
        }

        // Update is called once per frame
        void Update()
        {
        
        }

        private void PlayGame()
        {
            SceneManager.UnloadSceneAsync("MainMenu", UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
        }
    }
}
