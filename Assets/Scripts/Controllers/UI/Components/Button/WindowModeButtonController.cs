#region

using System;
using Controllers.State;
using Graphics;
using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Controllers.UI.Components.Button
{
    public class WindowModeButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    int final = (int) OptionsController.Current.WindowMode - 1;

                    OptionsController.Current.WindowMode = final >= 0
                        ? (WindowMode) final
                        : OptionsController.MaximumWindowModeValue;
                    break;
                case PointerEventData.InputButton.Right:
                    OptionsController.Current.WindowMode =
                        (WindowMode) (((int) OptionsController.Current.WindowMode + 1)
                                      % ((int) OptionsController.MaximumWindowModeValue + 1));
                    break;
                case PointerEventData.InputButton.Middle:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
