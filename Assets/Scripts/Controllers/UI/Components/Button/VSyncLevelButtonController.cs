#region

using Controllers.Game;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Button
{
    public class VSyncLevelButtonController : MonoBehaviour
    {
        private UnityEngine.UI.Button _VSyncLevelButton;

        private void Awake()
        {
            _VSyncLevelButton = GetComponent<UnityEngine.UI.Button>();
            _VSyncLevelButton.onClick.AddListener(ScrollVSyncLevel);
        }

        private static void ScrollVSyncLevel()
        {
            OptionsController.Current.VSyncLevel = (OptionsController.Current.VSyncLevel + 1) % 5;
        }
    }
}