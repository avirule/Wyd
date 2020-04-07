using UnityEngine;

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class MainCameraPitchTextController : UpdatingFormattedTextController
    {
        private Camera _MainCamera;
        private Transform _MainCameraTransform;

        protected override void Awake()
        {
            base.Awake();

            _MainCamera = Camera.main;

            if (_MainCamera != null)
            {
                _MainCameraTransform = _MainCamera.transform;
            }
        }

        protected override void TimedUpdate()
        {
            if (_MainCameraTransform == null)
            {
                return;
            }

            TextObject.text = string.Format(Format, _MainCameraTransform.eulerAngles.x);
        }
    }
}
