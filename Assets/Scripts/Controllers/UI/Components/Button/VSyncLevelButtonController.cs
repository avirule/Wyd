#region

using Controllers.State;
using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Controllers.UI.Components.Button
{
    public class VSyncLevelButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    int final = OptionsController.Current.VSyncLevel - 1;

                    OptionsController.Current.VSyncLevel = final >= 0 ? final : 4;
                    break;
                case PointerEventData.InputButton.Right:
                    OptionsController.Current.VSyncLevel = (OptionsController.Current.VSyncLevel + 1) % 5;
                    break;
            }
        }
    }
}
