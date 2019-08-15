#region

using Controllers.Game;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Controllers.UI.Components.Button
{
    public class VSyncLevelButtonController : MonoBehaviour
    {
        public UnityEngine.UI.Button VSyncLevelButton;
        public Text VSyncLevelButtonText;

        // Start is called before the first frame update
        private void Start()
        {
            VSyncLevelButton.onClick.AddListener(ScrollVSyncLevel);
        }

        private void ScrollVSyncLevel()
        {
            if (OptionsController.Current.VSyncLevel >= 4)
            {
                OptionsController.Current.VSyncLevel = 0;
            }
            else
            {
                OptionsController.Current.VSyncLevel++;
            }

            VSyncLevelButtonText.text = OptionsController.Current.VSyncLevel.ToString();
        }
    }
}