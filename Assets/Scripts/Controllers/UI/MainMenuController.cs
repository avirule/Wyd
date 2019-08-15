﻿#region

using Controllers.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#endregion

namespace Controllers.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public Text VersionText;

        // Start is called before the first frame update
        private void Start()
        {
            GameController.Current.ToggleCursorLocked(false);
            VersionText.text = Application.version;
        }

        public void PlayGame()
        {
            SceneManager.LoadSceneAsync("Scenes/Game", LoadSceneMode.Single);
            GameController.Current.ToggleCursorLocked(true);
        }
    }
}