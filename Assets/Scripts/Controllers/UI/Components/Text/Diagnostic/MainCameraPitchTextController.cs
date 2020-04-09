#region

using System;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class MainCameraPitchTextController : UpdatingFormattedTextController
    {
        private Camera _MainCamera;
        private Transform _MainCameraTransform;
        private float _LastEulerX;

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

            float eulerX = _MainCameraTransform.eulerAngles.x;

            if (Math.Abs(eulerX - _LastEulerX) < 0.1f)
            {
                return;
            }

            _LastEulerX = eulerX;
            TextObject.text = string.Format(Format, _LastEulerX);
        }
    }
}
