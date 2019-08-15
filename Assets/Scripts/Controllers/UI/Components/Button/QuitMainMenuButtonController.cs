#region

using Controllers.Game;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Button
{
    public class QuitMainMenuButtonController : MonoBehaviour
    {
        public UnityEngine.UI.Button QuitMainMenuButton;

        private void Start()
        {
            QuitMainMenuButton.onClick.AddListener(() => GameController.Current.QuitToMainMenu());
        }
    }
}