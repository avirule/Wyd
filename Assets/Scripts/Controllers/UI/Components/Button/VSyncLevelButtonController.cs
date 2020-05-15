#region

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class VSyncLevelButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    Options.Instance.VSync = !Options.Instance.VSync;
                    break;
                case PointerEventData.InputButton.Right:
                case PointerEventData.InputButton.Middle:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
