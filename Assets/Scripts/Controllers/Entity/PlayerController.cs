#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.UI;
using Wyd.Controllers.World;
using Wyd.Game.Entities;
using Wyd.Game.World.Blocks;
using Wyd.System;

#endregion

namespace Wyd.Controllers.Entity
{
    public class PlayerController : SingletonController<PlayerController>, IEntity
    {
        public const int REACH = 5;

        private static readonly TimeSpan _regularCheckWaitInterval = TimeSpan.FromSeconds(1d);
        private static readonly TimeSpan _minimumActionInterval = TimeSpan.FromSeconds(1f / 4f);

        private Ray _ReachRay;
        private RaycastHit _LastReachRayHit;
        private bool _IsInReachOfValidSurface;
        private Transform _ReachHitSurfaceObjectTransform;
        private float3 _Movement;
        private Stopwatch _ActionCooldown;
        private Stopwatch _RegularCheckWait;
        private float3 _Position;
        private int3 _ChunkPosition;

        [SerializeField]
        private Transform CameraTransform;

        [SerializeField]
        private Rigidbody Rigidbody;

        [SerializeField]
        private Collider Collider;

        [SerializeField]
        private InventoryController Inventory;

        [SerializeField]
        private GameObject ReachHitSurfaceObject;

        [SerializeField]
        private LayerMask GroundedMask;

        [SerializeField]
        private LayerMask RaycastLayerMask;

        [SerializeField]
        private float RotationSensitivity;

        [SerializeField]
        private float TravelSpeed;

        [SerializeField]
        private float JumpForce;

        [SerializeField]
        private bool Grounded;

        public Transform Transform { get; private set; }

        public int3 ChunkPosition
        {
            get => _ChunkPosition;
            private set
            {
                _ChunkPosition = value;
                OnChunkChanged(_ChunkPosition);
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

        public event EventHandler<float3> PositionChanged;
        public event EventHandler<int3> ChunkPositionChanged;
        public event EventHandler<IEntity> EntityDestroyed;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _ReachRay = new Ray();
            _ActionCooldown = Stopwatch.StartNew();
            _RegularCheckWait = Stopwatch.StartNew();

            Transform = transform;
            Tags = new HashSet<string>(new[]
            {
                "player",
                "loader",
                "collider"
            });

            ReachHitSurfaceObject = Instantiate(ReachHitSurfaceObject);
            _ReachHitSurfaceObjectTransform = ReachHitSurfaceObject.transform;

            ChunkPosition = new int3(int.MaxValue, int.MaxValue, int.MaxValue);

            CheckChangedPosition();
            CheckChangedChunkPosition();

            // PositionChanged += (sender, position) =>
            // {
            //     const int destruct_radius = 2;
            //     for (int x = -destruct_radius; x < (destruct_radius + 1); x++)
            //     {
            //         for (int y = -destruct_radius; y < (destruct_radius + 1); y++)
            //         {
            //             for (int z = -destruct_radius; z < (destruct_radius + 1); z++)
            //             {
            //                 float3 relativePosition = position + new float3(x, y, z);
            //
            //                 if (WorldController.Current.GetBlock(relativePosition, out ushort blockId)
            //                     && blockId != BlockController.AirID
            //                     && WorldController.Current.PlaceBlock(relativePosition, BlockController.AirID))
            //                 {
            //                     //Inventory.TryAdd(block.Id, 1);
            //                 }
            //             }
            //         }
            //     }
            // };
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
            CheckChangedChunkPosition();
        }

        private void Update()
        {
            if (InputController.Current.GetKey(KeyCode.F6))
            {
                Rigidbody.useGravity = !Rigidbody.useGravity;
            }

            UpdateMovement();
            CalculateJump();
            UpdateReachRay();
            UpdateLastLookAtCubeOrigin();
            CheckMouseClickActions();

            if (_RegularCheckWait.Elapsed > _regularCheckWaitInterval)
            {
                CheckChangedChunkPosition();

                _RegularCheckWait.Restart();
            }
        }

        private void OnDestroy()
        {
            OnEntityDestroyed();
        }


        #region UPDATE

        private void UpdateMovement()
        {
            _Movement.x = InputController.Current.GetAxisRaw("Horizontal");
            _Movement.z = InputController.Current.GetAxisRaw("Vertical");
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

            ushort blockId = WorldController.Current.GetBlock(math.all(math.csum(_LastReachRayHit.normal) > float3.zero)
                ? math.floor(_LastReachRayHit.point) - (float3)_LastReachRayHit.normal
                : math.floor(_LastReachRayHit.point));

            if (!BlockController.Current.CheckBlockHasProperty(blockId, BlockDefinition.Property.Destroyable))
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
            if (math.csum(normal) > 0f)
            {
                _ReachHitSurfaceObjectTransform.position =
                    (math.floor(_LastReachRayHit.point) - (math.abs(normal) * 0.4995f)) + new float3(0.5f);
            }
            else
            {
                _ReachHitSurfaceObjectTransform.position =
                    (math.floor(_LastReachRayHit.point) - (math.abs(normal) * 0.5005f)) + new float3(0.5f);
            }

            _ReachHitSurfaceObjectTransform.rotation = Quaternion.LookRotation(-normal);
        }

        public void CheckMouseClickActions()
        {
            if (InputController.Current.GetButton("LeftClick")
                && _IsInReachOfValidSurface
                && (_ActionCooldown.Elapsed > _minimumActionInterval))
            {
                int3 position;

                if (math.csum(_LastReachRayHit.normal) > 0f)
                {
                    WorldController.Current.PlaceBlock(position = WydMath.ToInt(math.floor(_LastReachRayHit.point) - (float3)_LastReachRayHit.normal),
                        BlockController.AirID);
                }
                else
                {
                    WorldController.Current.PlaceBlock(position = WydMath.ToInt(math.floor(_LastReachRayHit.point)), BlockController.AirID);
                }

                ushort blockId = WorldController.Current.GetBlock(position);

                if (BlockController.Current.CheckBlockHasProperty(blockId, BlockDefinition.Property.Collectible))
                {
                    Inventory.AddItem(blockId, 1);
                }

                _ActionCooldown.Restart();
            }

            if (InputController.Current.GetButton("RightClick")
                && _IsInReachOfValidSurface
                && !Collider.bounds.Contains(_LastReachRayHit.point)
                && (_ActionCooldown.Elapsed > _minimumActionInterval))
            {
                if (math.csum(_LastReachRayHit.normal) > 0f)
                {
                    WorldController.Current.PlaceBlock(WydMath.ToInt(math.floor(_LastReachRayHit.point)),
                        HotbarController.Current.SelectedId);
                }
                else
                {
                    WorldController.Current.PlaceBlock(
                        WydMath.ToInt(math.floor(_LastReachRayHit.point) + (float3)_LastReachRayHit.normal),
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

        private void CheckChangedChunkPosition()
        {
            float3 rounded = WydMath.RoundBy(Transform.position, ChunkController.SIZE);
            int3 chunkPosition = WydMath.ToInt(rounded);

            if (math.all(chunkPosition == ChunkPosition))
            {
                return;
            }

            ChunkPosition = chunkPosition;
        }

        #endregion


        #region MOVEMENT

        private void CalculateRotation()
        {
            //Rigidbody.MoveRotation(quaternion.Euler(0f, CameraTransform.rotation.eulerAngles.y, 0f));
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
            if (math.all(_Movement == float3.zero))
            {
                return;
            }

            float3 interpolatedMovement = TravelSpeed * Time.fixedDeltaTime * _Movement;
            float3 finalMovement = CameraTransform.TransformDirection(interpolatedMovement);

            // todo don't move relative to camera, it's fuckin dumb
            Rigidbody.MovePosition(Rigidbody.position + (Vector3)finalMovement);
        }

        #endregion


        #region Event Invocators

        private void OnPositionChanged(float3 newPosition)
        {
            PositionChanged?.Invoke(this, newPosition);
        }

        private void OnChunkChanged(int3 newChunkPosition)
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
