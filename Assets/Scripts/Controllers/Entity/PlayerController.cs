#region

using System;
using System.Collections.Generic;
using Controllers.World;
using Game.Entity;
using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class PlayerController : SingletonController<PlayerController>
    {
        public const int REACH = 5;

        private Transform _SelfTransform;
        private CapsuleCollider _Collider;
        private Ray _ReachRay;
        private RaycastHit _LastReachRayHit;
        private bool _IsInReachOfValidSurface;
        private Transform _ReachHitSurfaceObjectTransform;
        private Vector3 _Movement;
        private List<IEntityChunkChangedSubscriber> _EntityChangedChunkSubscribers;

        public Transform CameraTransform;
        public Rigidbody Rigidbody;
        public GameObject ReachHitSurfaceObject;
        public LayerMask GroundedMask;
        public LayerMask RaycastLayerMask;
        public Vector3 CurrentChunk;
        public float RotationSensitivity;
        public float TravelSpeed;
        public float JumpForce;
        public bool Grounded;

        private void Awake()
        {
            AssignCurrent(this);

            _SelfTransform = transform;
            _Collider = GetComponent<CapsuleCollider>();
            _ReachRay = new Ray();
            _EntityChangedChunkSubscribers = new List<IEntityChunkChangedSubscriber>();

            ReachHitSurfaceObject = Instantiate(ReachHitSurfaceObject);
            _ReachHitSurfaceObjectTransform = ReachHitSurfaceObject.transform;

            CurrentChunk.Set(int.MaxValue, 0, int.MaxValue);
        }

        private void Start()
        {
            WorldController.Current.RegisterEntity(_SelfTransform, REACH + 1);
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
            UpdateReachRay();
            UpdateLastLookAtCubeOrigin();

            if (Input.GetButton("Fire1") && _IsInReachOfValidSurface)
            {
                if (_LastReachRayHit.normal.Sum() > 0f)
                {
                    WorldController.Current.TryRemoveBlockAt(_LastReachRayHit.point.Floor() - _LastReachRayHit.normal);
                }
                else
                {
                    WorldController.Current.TryRemoveBlockAt(_LastReachRayHit.point.Floor());
                }
            }

            if (Input.GetButton("Fire2")
                && _IsInReachOfValidSurface
                && !_Collider.bounds.Contains(_LastReachRayHit.point))
            {
                if (_LastReachRayHit.normal.Sum() > 0f)
                {
                    WorldController.Current.TryPlaceBlockAt(_LastReachRayHit.point.Floor(), 9);
                }
                else
                {
                    WorldController.Current.TryPlaceBlockAt(_LastReachRayHit.point.Floor() + _LastReachRayHit.normal,
                        9);
                }
            }
        }

        private void CheckChangedChunk()
        {
            Vector3 chunkPosition = WorldController.GetChunkOriginFromPosition(_SelfTransform.position);
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


        private void UpdateReachRay()
        {
            _ReachRay.origin = CameraTransform.position;
            _ReachRay.direction = CameraTransform.forward;
        }

        private void UpdateLastLookAtCubeOrigin()
        {
            if (!Physics.Raycast(_ReachRay, out _LastReachRayHit, REACH, RaycastLayerMask))
            {
                ReachHitSurfaceObject.SetActive(false);
                _IsInReachOfValidSurface = false;
                return;
            }

            if (!ReachHitSurfaceObject.activeSelf)
            {
                ReachHitSurfaceObject.SetActive(true);
            }

            _IsInReachOfValidSurface = true;
            OrientReachHitSurfaceObject(_LastReachRayHit.normal);
        }

        private void OrientReachHitSurfaceObject(Vector3 normal)
        {
            if (normal.Sum() > 0f)
            {
                _ReachHitSurfaceObjectTransform.position =
                    (_LastReachRayHit.point.Floor() - (normal.Abs() * 0.4995f)) + Mathv.Half;
            }
            else
            {
                _ReachHitSurfaceObjectTransform.position =
                    (_LastReachRayHit.point.Floor() - (normal.Abs() * 0.5005f)) + Mathv.Half;
            }

            _ReachHitSurfaceObjectTransform.rotation = Quaternion.LookRotation(-normal);
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
            Grounded = Physics.Raycast(_SelfTransform.position, Vector3.down, _SelfTransform.localScale.y + 0.001f, GroundedMask);

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

            Rigidbody.MovePosition(Rigidbody.position + _SelfTransform.TransformDirection(modifiedMovement));
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
