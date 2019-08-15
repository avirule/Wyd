#region

using System;
using Controllers.World;
using Logging;
using NLog;
using Static;
using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Current;

        private Vector3 _Movement;
        public bool Grounded;
        public LayerMask GroundedMask;

        public float JumpForce;
        public Rigidbody Rigidbody;
        public float RotationSensitivity;
        public Transform RotationTransform;
        public float TravelSpeed;
        public Vector3Int CurrentChunk;

        public event EventHandler<Vector3Int> ChunkChanged;

        private void Awake()
        {
            if ((Current != default) && (Current != this))
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }

            CurrentChunk = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        }

        private void FixedUpdate()
        {
            CalculateRotation();
            CalculateMovement();
            
            CheckChangedChunk();
        }

        private void Update()
        {
            UpdateMovement();
            CalculateJump();

            if (Input.GetButton("Fire1"))
            {
                Transform self = transform;
                Ray ray = new Ray(self.position, self.eulerAngles);

                if (Physics.Raycast(ray, out RaycastHit hit, 50f, gameObject.layer))
                {
                    EventLog.Logger.Log(LogLevel.Info, hit.point);
                }
            }
        }

        private void UpdateMovement()
        {
            _Movement.x = Input.GetAxisRaw("Horizontal");
            _Movement.z = Input.GetAxisRaw("Vertical");
        }

        private void CalculateRotation()
        {
            Rigidbody.MoveRotation(Quaternion.Euler(0f, RotationTransform.eulerAngles.y, 0f));
        }

        private void CalculateJump()
        {
            Transform self = transform;
            Grounded = Physics.Raycast(self.position, Vector3.down, (self.localScale.y / 2f) + 0.001f, GroundedMask);

            if (Grounded && Input.GetButton("Jump"))
            {
                Rigidbody.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
            }
        }

        private void CalculateMovement()
        {
            if (_Movement == Vector3.zero)
            {
                return;
            }

            Vector3 modifiedMovement =
                (Grounded ? TravelSpeed : TravelSpeed * 0.5f) * Time.fixedDeltaTime * _Movement;

            Rigidbody.MovePosition(Rigidbody.position + transform.TransformDirection(modifiedMovement));
        }

        private void CheckChangedChunk()
        {
            Vector3Int chunkPosition =
                WorldController.GetWorldChunkOriginFromGlobalPosition(transform.position).ToInt();
            chunkPosition.y = 0;

            if (chunkPosition == CurrentChunk)
            {
                return;
            }

            CurrentChunk = chunkPosition;
            ChunkChanged?.Invoke(this, CurrentChunk);
        }
    }
}