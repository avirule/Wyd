#region

using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class GPUAccelerationButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            Options.Instance.GPUAcceleration = !Options.Instance.GPUAcceleration;
        }
    }
}
