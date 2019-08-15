#region

using Controllers.Game;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Button
{
    public class ExitButtonController : MonoBehaviour
    {
        public UnityEngine.UI.Button ExitButton;

        private void Start()
        {
            ExitButton.onClick.AddListener(() => GameController.ApplicationClose(0));
        }
    }
}