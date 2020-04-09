#region

using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Collections;
using NotImplementedException = System.NotImplementedException;

#endregion

namespace Wyd.Game
{
    public class CollisionLoader : MonoBehaviour, IPerFrameUpdate
    {
        private static readonly ObjectCache<GameObject> _ColliderCubeCache =
            new ObjectCache<GameObject>(false, -1, DeactivateGameObject);

        private static ref GameObject DeactivateGameObject(ref GameObject obj)
        {
            if (obj != default)
            {
                obj.SetActive(false);
            }

            return ref obj;
        }

        private Transform _SelfTransform;
        private int3 _LastCalculatedPosition;
        private Dictionary<float3, GameObject> _ColliderCubes;
        private bool _ScheduledRecalculation;

        public GameObject CollisionCubeObject;
        public Transform AttachedTransform;
        private int _Radius;

        public Volume BoundingBox { get; private set; }

        public int Radius
        {
            get => _Radius;
            set
            {
                if (_Radius == value)
                {
                    return;
                }

                _Radius = value;
                _ScheduledRecalculation = true;
            }
        }

        private void Awake()
        {
            _SelfTransform = transform;
            _ColliderCubes = new Dictionary<float3, GameObject>();
            _LocalCacheQueue = new Queue<float3>();
        }

        private void Start()
        {
            // always do an initial pass to create colliders
            _ScheduledRecalculation = true;
        }

        private void OnEnable()
        {
            PerFrameUpdateController.Current.RegisterPerFrameUpdater(70, this);

            WorldController.Current.ChunkMeshChanged += OnChunkMeshChanged;
        }

        private void OnDisable()
        {
            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(70, this);

            WorldController.Current.ChunkMeshChanged -= OnChunkMeshChanged;
        }

        public void FrameUpdate()
        {
            float3 difference = math.abs(_LastCalculatedPosition - (float3)AttachedTransform.position);

            if (_ScheduledRecalculation || math.any(difference >= 1))
            {
                _LastCalculatedPosition = WydMath.ToInt(math.floor(AttachedTransform.position));

                Recalculate();
            }
        }

        private void Recalculate()
        {
            RecalculateBoundingBox();
            CalculateColliders();
            CullOutOfBoundsSurfaces();

            //UpdatedMesh?.Invoke(this, Mesh);
            _ScheduledRecalculation = false;
        }

        private void RecalculateBoundingBox()
        {
            // +1 to include center blocks / position
            int size = (Radius * 2) + 1;

            BoundingBox = new Volume(_LastCalculatedPosition, new float3(size, size, size));
        }

        private void CalculateColliders()
        {
            for (int x = -Radius; x < (Radius + 1); x++)
            {
                for (int y = -Radius; y < (Radius + 1); y++)
                {
                    for (int z = -Radius; z < (Radius + 1); z++)
                    {
                        int3 localPosition = new int3(x, y, z);
                        int3 globalPosition = _LastCalculatedPosition + localPosition;
                        float3 trueCenterGlobalPosition = globalPosition + new float3(0.5f);

                        if (!WorldController.Current.TryGetBlockAt(globalPosition, out ushort blockId)
                            || (blockId == BlockController.AIR_ID)
                            || !BlockController.Current.CheckBlockHasProperties(blockId,
                                BlockDefinition.Property.Collideable))
                        {
                            if (_ColliderCubes.ContainsKey(trueCenterGlobalPosition))
                            {
                                _LocalCacheQueue.Enqueue(trueCenterGlobalPosition);
                            }

                            continue;
                        }

                        if (_ColliderCubes.ContainsKey(trueCenterGlobalPosition))
                        {
                            continue;
                        }

                        GameObject collisionCube = GetCollisionCube(trueCenterGlobalPosition);
                        _ColliderCubes.Add(trueCenterGlobalPosition, collisionCube);
                    }
                }
            }
        }

        private Queue<float3> _LocalCacheQueue;

        private void CullOutOfBoundsSurfaces()
        {
            foreach (float3 position in _ColliderCubes.Keys.Where(position => !BoundingBox.Contains(position)))
            {
                _LocalCacheQueue.Enqueue(position);
            }

            while (_LocalCacheQueue.Count > 0)
            {
                float3 position = _LocalCacheQueue.Dequeue();
                GameObject obj = _ColliderCubes[position];
                _ColliderCubes.Remove(position);
                _ColliderCubeCache.CacheItem(ref obj);
            }
        }

        private GameObject GetCollisionCube(float3 position)
        {
            if (!_ColliderCubeCache.TryRetrieve(out GameObject surfaceCollider))
            {
                surfaceCollider = Instantiate(CollisionCubeObject);
            }

            Transform surfaceColliderTransform = surfaceCollider.transform;
            surfaceColliderTransform.parent = _SelfTransform;
            surfaceColliderTransform.position = position;
            surfaceCollider.SetActive(true);

            return surfaceCollider;
        }

        private void OnChunkMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            _ScheduledRecalculation = true;
        }
    }
}
