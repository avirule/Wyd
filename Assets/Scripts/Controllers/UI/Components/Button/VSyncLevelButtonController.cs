#region

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class VSyncLevelButtonController : MonoBehaviour, IPointerClickHandler
    {
        private const int _MAXIMUM_VSYNC_LEVEL = 1;
        
        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    int final = OptionsController.Current.VSyncLevel - 1;

                    OptionsController.Current.VSyncLevel = final >= 0 ? final : _MAXIMUM_VSYNC_LEVEL;
                    break;
                case PointerEventData.InputButton.Right:
                    OptionsController.Current.VSyncLevel = (OptionsController.Current.VSyncLevel + 1) % (_MAXIMUM_VSYNC_LEVEL + 1);
                    break;
                case PointerEventData.InputButton.Middle:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
