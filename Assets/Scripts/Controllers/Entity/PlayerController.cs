#region

using System.Collections.Generic;
using Controllers.World;
using Game.Entity;
using Graphics;
using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class PlayerController : SingletonController<PlayerController>
    {
        public const int REACH = 5;

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

            _Collider = GetComponent<CapsuleCollider>();
            _ReachRay = new Ray();
            _EntityChangedChunkSubscribers = new List<IEntityChunkChangedSubscriber>();

            ReachHitSurfaceObject = Instantiate(ReachHitSurfaceObject);
            _ReachHitSurfaceObjectTransform = ReachHitSurfaceObject.transform;
            
            CurrentChunk.Set(int.MaxValue, 0, int.MaxValue);
        }

        private void Start()
        {
            WorldController.Current.RegisterEntity(transform, REACH + 1);
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
                WorldController.Current.TryRemoveBlockAt(_LastReachRayHit.point.Floor() + -_LastReachRayHit.normal);
            }

            if (Input.GetButton("Fire2") && _IsInReachOfValidSurface && !_Collider.bounds.Contains(_LastReachRayHit.point))
            {
                WorldController.Current.TryPlaceBlockAt(_LastReachRayHit.point.Floor(), 9);
            }
        }

        private void CheckChangedChunk()
        {
            Vector3 chunkPosition = WorldController.GetChunkOriginFromPosition(transform.position);
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

            Vector3 absoluteNormal = _LastReachRayHit.normal.Abs();
            _ReachHitSurfaceObjectTransform.position = _LastReachRayHit.point.Floor() - (absoluteNormal * 0.4995f) + Mathv.Half;
            _ReachHitSurfaceObjectTransform.rotation = Quaternion.FromToRotation(Vector3.back, absoluteNormal );
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
