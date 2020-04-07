#region

using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.Graphics
{
    public class CameraController : MonoBehaviour
    {
        private Transform _SelfTransform;
        private Camera _Camera;
        private float _Pitch;
        private float _Yaw;

        private void Start()
        {
            _SelfTransform = transform;
            _Camera = GetComponent<Camera>();
            _Camera.depthTextureMode = DepthTextureMode.None;
        }

        private void FixedUpdate()
        {
            // todo limit vertical rotation
            _SelfTransform.rotation = quaternion.Euler(-_Pitch * Time.fixedDeltaTime, _Yaw * Time.fixedDeltaTime, 0f);
        }

        private void Update()
        {
            float axisY = InputController.Current.GetAxis("Mouse Y");
            float axisX = InputController.Current.GetAxis("Mouse X");

            _Pitch += 2 * axisY;
            _Yaw += 2 * axisX;
        }
    }
}
