using System;
using Controllers.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Controllers.UI.Components
{
    public class QuitMainMenuButtonController : MonoBehaviour
    {
        public Button QuitMainMenuButton;
        
        private void Start()
        {
            QuitMainMenuButton.onClick.AddListener(() => GameController.Current.QuitToMainMenu());
        }
    }
}