#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Controllers.State;
using Controllers.UI;
using Controllers.World;
using Game.Entities;
using Game.World.Blocks;
using Game.World.Chunks;
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
        private Vector3 _Position;
        private Vector3 _CurrentChunk;

        public Transform CameraTransform;
        public InventoryController Inventory;
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

        public Vector3 CurrentChunk
        {
            get => _CurrentChunk;
            private set
            {
                _CurrentChunk = value;
                OnChunkChanged(_CurrentChunk);
            }
        }

        public Vector3 Position
        {
            get => _Position;
            private set
            {
                _Position = value;
                OnPositionChanged(_Position);
            }
        }

        public HashSet<string> Tags { get; private set; }

        public event EventHandler<Vector3> PositionChanged;
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
            Tags = new HashSet<string>(new[]
            {
                "primary",
                "player",
                "loader",
                "collider",
                "collector"
            });

            ReachHitSurfaceObject = Instantiate(ReachHitSurfaceObject);
            _ReachHitSurfaceObjectTransform = ReachHitSurfaceObject.transform;

            CurrentChunk.Set(int.MaxValue, 0, int.MaxValue);

            // todo fix this
//            PositionChanged += (sender, position) =>
//            {
//                const int destruct_radius = 2;
//                for (int x = -destruct_radius; x < (destruct_radius + 1); x++)
//                {
//                    for (int y = -destruct_radius; y < (destruct_radius + 1); y++)
//                    {
//                        for (int z = -destruct_radius; z < (destruct_radius + 1); z++)
//                        {
//                            Vector3 relativePosition = position + new Vector3(x, y, z);
//
//                            if (WorldController.Current.TryGetBlockAt(relativePosition, out Block block)
//                                && BlockController.Current.TryGetBlockRule(block.Id, out IReadOnlyBlockRule blockRule)
//                                && blockRule.Destroyable
//                                && WorldController.Current.TryRemoveBlockAt(relativePosition)
//                                && blockRule.Collectible)
//                            {
//                                Inventory.AddItem(block.Id, 1);
//                            }
//                        }
//                    }
//                }
//            };
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
            CheckChangedPosition();
            CheckChangedChunk();
        }

        private void Update()
        {
            UpdateMovement();
            CalculateJump();
            UpdateReachRay();
            UpdateLastLookAtCubeOrigin();
            CheckMouseClickActions();

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

        #region UPDATE

        private void UpdateReachRay()
        {
            _ReachRay.origin = CameraTransform.position;
            _ReachRay.direction = CameraTransform.forward;
        }

        private void UpdateLastLookAtCubeOrigin()
        {
            if (!Physics.Raycast(_ReachRay, out _LastReachRayHit, REACH, RaycastLayerMask)
                || !WorldController.Current.TryGetBlockAt(
                    _LastReachRayHit.normal.Sum() > 0f
                        ? _LastReachRayHit.point.Floor() - _LastReachRayHit.normal
                        : _LastReachRayHit.point.Floor(), out Block block)
                || (!BlockController.Current.GetBlockRule(block.Id)?.Destroyable ?? false))
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

        public void CheckMouseClickActions()
        {
            if (InputController.Current.GetButton("LeftClick")
                && _IsInReachOfValidSurface
                && (_ActionCooldown.Elapsed > MinimumActionInterval))
            {
                Vector3 position;

                if (((_LastReachRayHit.normal.Sum() > 0f)
                     && WorldController.Current.TryRemoveBlockAt(
                         position = _LastReachRayHit.point.Floor() - _LastReachRayHit.normal))
                    || WorldController.Current.TryRemoveBlockAt(
                        position = _LastReachRayHit.point.Floor()))
                {
                    WorldController.Current.TryGetBlockAt(position, out Block destroyedBlock);

                    if (BlockController.Current.GetBlockRule(destroyedBlock.Id)?.Collectible ?? false)
                    {
                        Inventory.AddItem(destroyedBlock.Id, 1);
                    }
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
                    WorldController.Current.TryPlaceBlockAt(_LastReachRayHit.point.Floor(),
                        HotbarController.Current.SelectedId);
                }
                else
                {
                    WorldController.Current.TryPlaceBlockAt(_LastReachRayHit.point.Floor() + _LastReachRayHit.normal,
                        HotbarController.Current.SelectedId);
                }

                _ActionCooldown.Restart();
            }
        }

        #endregion

        #region FIXED UPDATE

        private void CheckChangedPosition()
        {
            Vector3 position = Transform.position;

            if (Position != position)
            {
                Position = position;
            }
        }

        private void CheckChangedChunk()
        {
            Vector3 chunkRegionPosition = WorldController.GetNearestVector3RoundedBy(Transform.position, ChunkRegionController.Size);
            chunkRegionPosition.y = 0;

            if (chunkRegionPosition == CurrentChunk)
            {
                return;
            }

            CurrentChunk = chunkRegionPosition;
        }

        #endregion

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
            Grounded = Physics.Raycast(Transform.position, Vector3.down, Transform.localScale.y + 0.00001f,
                GroundedMask);

            if (Grounded && InputController.Current.GetButton("Jump"))
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

            Rigidbody.MovePosition(Rigidbody.position + Transform.TransformDirection(modifiedMovement));
        }

        #endregion


        #region Event Invocators

        private void OnPositionChanged(Vector3 newPosition)
        {
            PositionChanged?.Invoke(this, newPosition);
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
