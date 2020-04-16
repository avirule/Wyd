#region

using System;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class MainCameraYawTextController : UpdatingFormattedTextController
    {
        private Camera _MainCamera;
        private Transform _MainCameraTransform;
        private float _LastEulerY;

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

            float eulerY = _MainCameraTransform.eulerAngles.y;

            if (Math.Abs(eulerY - _LastEulerY) < 0.1f)
            {
                return;
            }

            _LastEulerY = eulerY;
            _TextObject.text = string.Format(_Format, _LastEulerY);
        }
    }
}
