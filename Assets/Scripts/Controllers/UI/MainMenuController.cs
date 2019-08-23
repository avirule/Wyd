#region

using Controllers.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Controllers.UI
{
    public class MainMenuController : MonoBehaviour
    {
        // Start is called before the first frame update
        private void Start()
        {
            GameController.Current.ToggleCursorLocked(false);
        }

        public void PlayGame()
        {
            SceneManager.LoadSceneAsync("Scenes/Game", LoadSceneMode.Single);
            GameController.Current.ToggleCursorLocked(true);
        }
    }
}