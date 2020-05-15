#region

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Controllers.State;
using Wyd.Extensions;
using Wyd.Graphics;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class WindowModeButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    Options.Instance.FullScreenMode = Options.Instance.FullScreenMode.Next();
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
