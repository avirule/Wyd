#region

using Controllers.State;
using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Controllers.UI.Components.Button
{
    public class GPUAccelerationButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            OptionsController.Current.GPUAcceleration = !OptionsController.Current.GPUAcceleration;
        }
    }
}
