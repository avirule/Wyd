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

        private void ScrollVSyncLevel()
        {
            if (OptionsController.Current.VSyncLevel == 4)
            {
                OptionsController.Current.VSyncLevel = 0;
            }
            else
            {
                OptionsController.Current.VSyncLevel++;
            }
        }
    }
}