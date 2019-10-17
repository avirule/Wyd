#region

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Controllers.State;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class ThreadingModeButtonController : MonoBehaviour, IPointerClickHandler
    {
        private static readonly int ThreadingModeEnumLength = Enum.GetNames(typeof(ThreadingMode)).Length;

        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    int final = (int)OptionsController.Current.ThreadingMode - 1;

                    OptionsController.Current.ThreadingMode =
                        (ThreadingMode)(final >= 0 ? final : ThreadingModeEnumLength - 1);
                    break;
                case PointerEventData.InputButton.Right:
                    OptionsController.Current.ThreadingMode =
                        (ThreadingMode)(((int)OptionsController.Current.ThreadingMode + 1) % ThreadingModeEnumLength);
                    break;
                case PointerEventData.InputButton.Middle:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
