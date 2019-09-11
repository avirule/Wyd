#region

using Controllers.State;
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
            InputController.Current.ToggleCursorLocked(false);
        }

        public void PlayGame()
        {
            SceneManager.LoadSceneAsync("Scenes/Game", LoadSceneMode.Single);
        }
    }
}
