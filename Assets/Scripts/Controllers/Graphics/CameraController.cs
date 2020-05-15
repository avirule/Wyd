#region

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
            Quaternion rotation = _SelfTransform.rotation;
            _SelfTransform.rotation = Quaternion.Euler(-_Pitch * Time.fixedDeltaTime, _Yaw * Time.fixedDeltaTime, rotation.z);
        }

        private void Update()
        {
            float axisY = InputController.Current.GetAxis("Mouse Y");
            float axisX = InputController.Current.GetAxis("Mouse X");

            _Pitch += 40f * axisY;
            _Yaw += 40f * axisX;
        }
    }
}
