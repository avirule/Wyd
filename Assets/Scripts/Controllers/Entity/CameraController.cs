#region

using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class CameraController : MonoBehaviour
    {
        private float _Pitch;
        private float _RotationSensitivity;
        private float _Yaw;

        private void Start()
        {
            _RotationSensitivity =
                GameObject.FindWithTag("Player").GetComponent<PlayerController>().RotationSensitivity;
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
            _Pitch += _RotationSensitivity * Input.GetAxis("Mouse Y");
            _Yaw += _RotationSensitivity * Input.GetAxis("Mouse X");
        }
    }
}