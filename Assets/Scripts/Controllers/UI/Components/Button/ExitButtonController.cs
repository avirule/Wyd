#region

using Controllers.Game;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Button
{
    public class ExitButtonController : MonoBehaviour
    {
        private UnityEngine.UI.Button _ExitButton;

        private void Awake()
        {
            _ExitButton = GetComponent<UnityEngine.UI.Button>();
            _ExitButton.onClick.AddListener(() => GameController.ApplicationClose(0));
        }
    }
}