#region

using Controllers.Game;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Button
{
    public class QuitMainMenuButtonController : MonoBehaviour
    {
        private UnityEngine.UI.Button _QuitMainMenuButton;

        private void Awake()
        {
            _QuitMainMenuButton = GetComponent<UnityEngine.UI.Button>();
            _QuitMainMenuButton.onClick.AddListener(() => GameController.Current.QuitToMainMenu());
        }
    }
}