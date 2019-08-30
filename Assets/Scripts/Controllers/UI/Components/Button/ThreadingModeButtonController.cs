#region

using Controllers.Game;
using Game.World;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Button
{
    public class ThreadingModeButtonController : MonoBehaviour
    {
        private UnityEngine.UI.Button _ThreadingModeButton;

        private void Awake()
        {
            _ThreadingModeButton = GetComponent<UnityEngine.UI.Button>();
            _ThreadingModeButton.onClick.AddListener(ScrollThreadingMode);
        }

        private static void ScrollThreadingMode()
        {
            OptionsController.Current.ThreadingMode =
                (ThreadingMode) ((int) (OptionsController.Current.ThreadingMode + 1) % 3);
        }
    }
}