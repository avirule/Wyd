#region

using Controllers.State;
using Extensions;
using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Controllers.UI.Components.Button
{
    public class VSyncLevelButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            int step = eventData.GetButtonAsInt();

            if ((step == -1) && (OptionsController.Current.VSyncLevel == 0))
            {
                OptionsController.Current.VSyncLevel = 4;
            }
            else
            {
                OptionsController.Current.VSyncLevel = (OptionsController.Current.VSyncLevel + step) % 5;
            }
        }
    }
}
