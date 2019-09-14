#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Controllers.State;
using Controllers.World;
using Game.Entities;
using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class PlayerController : SingletonController<PlayerController>, IEntity, ICollideable
    {
        public const int REACH = 5;

        private static readonly TimeSpan RegularCheckWaitInterval = TimeSpan.FromSeconds(1d);
        private static readonly TimeSpan MinimumActionInterval = TimeSpan.FromSeconds(1f / 4f);

        private Ray _ReachRay;
        private RaycastHit _LastReachRayHit;
        private bool _IsInReachOfValidSurface;
        private Transform _ReachHitSurfaceObjectTransform;
        private Vector3 _Movement;
        private Stopwatch _ActionCooldown;
        private Stopwatch _RegularCheckWait;

        public Transform CameraTransform;
        public GameObject ReachHitSurfaceObject;
        public LayerMask GroundedMask;
        public LayerMask RaycastLayerMask;
        public float RotationSensitivity;
        public float TravelSpeed;
        public float JumpForce;
        public bool Grounded;

        public Transform Transform { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public Collider Collider { get; private set; }
        public Vector3 CurrentChunk { get; private set; }
        public IReadOnlyList<string> Tags { get; private set; }

        public event EventHandler<Vector3> CausedPositionChanged;
        public event EventHandler<Vector3> ChunkPositionChanged;
        public event EventHandler<IEntity> EntityDestroyed;

        private void Awake()
        {
            AssignCurrent(this);

            _ReachRay = new Ray();
            _ActionCooldown = Stopwatch.StartNew();
            _RegularCheckWait = Stopwatch.StartNew();

            Transform = transform;
            Rigidbody = GetComponent<Rigidbody>();
            Collider = GetComponent<CapsuleCollider>();
            Tags = new ReadOnlyCollection<string>(new List<string>
            {
                "player",
                "loader",
                "collider"
            });

            ReachHitSurfaceObject = Instantiate(ReachHitSurfaceObject);
            _ReachHitSurfaceObjectTransform = ReachHitSurfaceObject.transform;

            CurrentChunk.Set(int.MaxValue, 0, int.MaxValue);
        }

        private void Start()
        {
            InputController.Current.ToggleCursorLocked(true);
            EntityController.Current.RegisterEntity(GetType(), this);
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

            if (InputController.Current.GetButton("LeftClick")
                && _IsInReachOfValidSurface
                && (_ActionCooldown.Elapsed > MinimumActionInterval))
            {
                if (_LastReachRayHit.normal.Sum() > 0f)
                {
                    WorldController.Current.TryRemoveBlockAt(_LastReachRayHit.point.Floor() - _LastReachRayHit.normal);
                }
                else
                {
                    WorldController.Current.TryRemoveBlockAt(_LastReachRayHit.point.Floor());
                }

                _ActionCooldown.Restart();
            }

            if (InputController.Current.GetButton("RightClick")
                && _IsInReachOfValidSurface
                && !Collider.bounds.Contains(_LastReachRayHit.point)
                && (_ActionCooldown.Elapsed > MinimumActionInterval))
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

                _ActionCooldown.Restart();
            }

            if (_RegularCheckWait.Elapsed > RegularCheckWaitInterval)
            {
                CheckChangedChunk();

                _RegularCheckWait.Restart();
            }
        }

        private void OnDestroy()
        {
            OnEntityDestroyed();
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

        private void CheckChangedChunk()
        {
            Vector3 chunkPosition = WorldController.GetChunkOriginFromPosition(Transform.position);
            chunkPosition.y = 0;

            if (chunkPosition == CurrentChunk)
            {
                return;
            }

            CurrentChunk = chunkPosition;
            OnChunkChanged(CurrentChunk);
        }

        #region MOVEMENT

        private void UpdateMovement()
        {
            _Movement.x = InputController.Current.GetAxisRaw("Horizontal");
            _Movement.z = InputController.Current.GetAxisRaw("Vertical");
        }

        private void CalculateRotation()
        {
            Rigidbody.MoveRotation(Quaternion.Euler(0f, CameraTransform.eulerAngles.y, 0f));
        }

        private void CalculateJump()
        {
            Grounded = Physics.Raycast(Transform.position, Vector3.down, Transform.localScale.y + 0.001f,
                GroundedMask);

            if (Grounded && InputController.Current.GetButton("Jump"))
            {
                Rigidbody.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
            }

            OnCausedPositionChanged(Transform.position);
        }

        private void CalculateMovement()
        {
            if (_Movement == Vector3.zero)
            {
                return;
            }

            Vector3 modifiedMovement = TravelSpeed * Time.fixedDeltaTime * _Movement;

            Rigidbody.MovePosition(Rigidbody.position + Transform.TransformDirection(modifiedMovement));
            OnCausedPositionChanged(Transform.position);
        }

        #endregion


        #region Event Invocators

        private void OnCausedPositionChanged(Vector3 newPosition)
        {
            CausedPositionChanged?.Invoke(this, newPosition);
        }

        private void OnChunkChanged(Vector3 newChunkPosition)
        {
            ChunkPositionChanged?.Invoke(this, newChunkPosition);
        }

        private void OnEntityDestroyed()
        {
            EntityDestroyed?.Invoke(this, this);
        }

        #endregion
    }
}
