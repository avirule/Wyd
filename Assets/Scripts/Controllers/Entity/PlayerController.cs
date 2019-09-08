#region

using System.Collections.Generic;
using Controllers.World;
using Game.Entity;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class PlayerController : SingletonController<PlayerController>
    {
        public const int REACH = 5;

        private Vector3 _Movement;
        private List<IEntityChunkChangedSubscriber> _EntityChangedChunkSubscribers;

        public bool Grounded;
        public LayerMask GroundedMask;
        public LayerMask RaycastLayerMask;

        public float JumpForce;
        public Rigidbody Rigidbody;
        public float RotationSensitivity;
        public Transform CameraTransform;
        public float TravelSpeed;
        public Vector3Int CurrentChunk;

        private void Awake()
        {
            AssignCurrent(this);

            _EntityChangedChunkSubscribers = new List<IEntityChunkChangedSubscriber>();

            CurrentChunk.Set(int.MaxValue, 0, int.MaxValue);
        }

        private void Start()
        {
            WorldController.Current.RegisterEntity(transform, 2);
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
                Ray ray = new Ray(CameraTransform.position, CameraTransform.eulerAngles);

                if (Physics.Raycast(ray, out RaycastHit hit, REACH, RaycastLayerMask))
                {
                    EventLog.Logger.Log(LogLevel.Info, hit.point);
                }
            }
        }

        private void CheckChangedChunk()
        {
            Vector3Int chunkPosition =
                WorldController.GetChunkOriginFromPosition(transform.position).ToInt();
            chunkPosition.y = 0;

            if (chunkPosition == CurrentChunk)
            {
                return;
            }

            CurrentChunk = chunkPosition;
            FlagChangedChunk();
        }

        public void RegisterEntityChangedSubscriber(IEntityChunkChangedSubscriber subscriber)
        {
            _EntityChangedChunkSubscribers.Add(subscriber);
        }


        #region MOVEMENT

        private void UpdateMovement()
        {
            _Movement.x = Input.GetAxisRaw("Horizontal");
            _Movement.z = Input.GetAxisRaw("Vertical");
        }

        private void CalculateRotation()
        {
            Rigidbody.MoveRotation(Quaternion.Euler(0f, CameraTransform.eulerAngles.y, 0f));
        }

        private void CalculateJump()
        {
            Transform self = transform;
            Grounded = Physics.Raycast(self.position, Vector3.down, self.localScale.y + 0.001f, GroundedMask);

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

            Vector3 modifiedMovement = TravelSpeed * Time.fixedDeltaTime * _Movement;

            Rigidbody.MovePosition(Rigidbody.position + transform.TransformDirection(modifiedMovement));
        }

        #endregion


        #region EVENTS

        private void FlagChangedChunk()
        {
            foreach (IEntityChunkChangedSubscriber subscriber in _EntityChangedChunkSubscribers)
            {
                subscriber.PrimaryLoaderChangedChunk = true;
            }
        }

        #endregion
    }
}