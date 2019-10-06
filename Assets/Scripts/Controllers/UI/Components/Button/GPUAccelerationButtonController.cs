#region

using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class GPUAccelerationButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            OptionsController.Current.GPUAcceleration = !OptionsController.Current.GPUAcceleration;
        }
    }
}
