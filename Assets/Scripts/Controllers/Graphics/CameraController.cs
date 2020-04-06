#region

using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.Entity;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.Graphics
{
    public class CameraController : MonoBehaviour
    {
        private Transform _SelfTransform;
        private Camera _Camera;
        private float _Pitch;
        private float _RotationSensitivity;
        private float _Yaw;

        private void Start()
        {
            _SelfTransform = transform;
            _Camera = GetComponent<Camera>();
            _Camera.depthTextureMode = DepthTextureMode.None;
            _RotationSensitivity = PlayerController.Current.RotationSensitivity;
        }

        private void FixedUpdate()
        {
            _SelfTransform.rotation =
                quaternion.Euler(new float3(math.clamp(-_Pitch * Time.fixedDeltaTime, -90f, 90f),
                    _Yaw * Time.fixedDeltaTime, 0f));
        }

        private void Update()
        {
            float axisY = InputController.Current.GetAxis("Mouse Y");
            float axisX = InputController.Current.GetAxis("Mouse X");

            _Pitch += _RotationSensitivity * axisY;
            _Yaw += _RotationSensitivity * axisX;
        }
    }
}
