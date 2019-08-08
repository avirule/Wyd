using UnityEngine;

namespace Entities
{
    public class Player : MonoBehaviour
    {
        private float _InputX;
        private float _InputY;
        private float _Yaw;
        public float JumpHeight;

        public float TravelSpeed;

        public void FixedUpdate()
        {
            float rotation = _Yaw * Time.fixedDeltaTime;

            transform.Rotate(0f, _Yaw, 0f);

            float movementX = _InputX * TravelSpeed * Time.fixedDeltaTime;
            float movementZ = _InputY * TravelSpeed * Time.fixedDeltaTime;

            transform.Translate(movementX, 0f, movementZ);
        }

        public void Update()
        {
            _Yaw += 20f * Input.GetAxis("Mouse X");
            _InputX = Input.GetAxis("Horizontal");
            _InputY = Input.GetAxis("Vertical");
        }
    }
}