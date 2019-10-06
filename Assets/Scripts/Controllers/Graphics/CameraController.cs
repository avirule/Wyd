#region

using UnityEngine;
using Wyd.Controllers.Entity;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.Graphics
{
    public class CameraController : MonoBehaviour
    {
        private Camera _Camera;
        private float _Pitch;
        private float _RotationSensitivity;
        private float _Yaw;

        private void Start()
        {
            _Camera = GetComponent<Camera>();
            _Camera.depthTextureMode = DepthTextureMode.None;
            _RotationSensitivity = PlayerController.Current.RotationSensitivity;
        }

        private void FixedUpdate()
        {
            Transform self = transform;

            self.rotation =
                Quaternion.Euler(new Vector3(Mathf.Clamp(-_Pitch * Time.fixedDeltaTime, -90f, 90f),
                    _Yaw * Time.fixedDeltaTime, 0f));
        }

        private void Update()
        {
            _Pitch += _RotationSensitivity * InputController.Current.GetAxis("Mouse Y");
            _Yaw += _RotationSensitivity * InputController.Current.GetAxis("Mouse X");
        }
    }
}
