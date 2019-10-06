#region

using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class QuitMainMenuButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                GameController.QuitToMainMenu();
            }
        }
    }
}
