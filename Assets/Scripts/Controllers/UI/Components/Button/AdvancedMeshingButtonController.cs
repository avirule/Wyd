#region

using UnityEngine;
using UnityEngine.EventSystems;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.UI.Components.Button
{
    public class AdvancedMeshingButtonController : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            Options.Instance.AdvancedMeshing = !Options.Instance.AdvancedMeshing;
        }
    }
}
