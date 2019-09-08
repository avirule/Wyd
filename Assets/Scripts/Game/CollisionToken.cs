#region

using System.Collections.Generic;
using System.Linq;
using Controllers.World;
using UnityEngine;

#endregion

namespace Game
{
    public class CollisionToken : MonoBehaviour
    {
        private static readonly Vector3 PivotOffset = new Vector3(0.5f, 0.5f, 0.5f);
        private static readonly ObjectCache<GameObject> CubeCache = new ObjectCache<GameObject>(DeactivateCube);

        private Transform _SelfTransform;
        private Dictionary<Vector3, GameObject> _CubeColliders;
        private Queue<Vector3> _DeactivationQueue;
        private bool _ScheduledRecalculation;

        public GameObject CubeObject;
        public Transform AttachedTransform;

        public Mesh Mesh { get; private set; }
        public Bounds BoundingBox { get; private set; }
        public int Radius { get; set; }

        private void Awake()
        {
            _SelfTransform = transform;
            _CubeColliders = new Dictionary<Vector3, GameObject>();
            _DeactivationQueue = new Queue<Vector3>();

            AttachedTransform = _SelfTransform.parent;
            _SelfTransform.position = AttachedTransform.position.Floor();
            RecalculateBoundingBox();
            Recalculate();
        }

        private void Update()
        {
            ProcessDeactivationQueue();

            if (AttachedTransform == default)
            {
                return;
            }

            Vector3 difference = (_SelfTransform.position - AttachedTransform.position).Abs();

            if (!Mathv.AnyGreaterThanVector3(difference, Vector3.one) && !_ScheduledRecalculation)
            {
                return;
            }

            _SelfTransform.position = AttachedTransform.position.Floor();

            Recalculate();
        }

        private void OnDestroy()
        {
            Destroy(Mesh);
        }

        private static GameObject DeactivateCube(GameObject cube)
        {
            cube.SetActive(false);
            return cube;
        }

        private void Recalculate()
        {
            RecalculateBoundingBox();
            BuildCubeTerrainMirror();
            QueueOutOfRangeCubes();

            // todo use meshing to handle special block shapes
            //UpdatedMesh?.Invoke(this, Mesh);
            _ScheduledRecalculation = false;
        }

        private GameObject GetNewCube(Vector3 position)
        {
            // todo add support for special bounds

            GameObject cube = CubeCache.RetrieveItem() ?? Instantiate(CubeObject);

            cube.SetActive(true);

            Transform cubeTransform = cube.transform;
            cubeTransform.parent = _SelfTransform;
            cubeTransform.position = position;
            
            return cube;
        }

        private void BuildCubeTerrainMirror()
        {
            for (int x = -Radius; x < (Radius + 1); x++)
            {
                for (int y = -Radius; y < (Radius + 1); y++)
                {
                    for (int z = -Radius; z < (Radius + 1); z++)
                    {
                        Vector3 localPosition = new Vector3(x, y, z);
                        Vector3 globalPosition = _SelfTransform.position + localPosition;

                        bool cubeExistsAtPosition = _CubeColliders.ContainsKey(globalPosition);

                        if (cubeExistsAtPosition || !WorldController.Current.GetBlockAt(globalPosition).HasAnyFace())
                        {
                            if (cubeExistsAtPosition)
                            {
                                _DeactivationQueue.Enqueue(globalPosition);
                            }

                            continue;
                        }

                        GameObject cube = GetNewCube(globalPosition + PivotOffset);

                        _CubeColliders.Add(globalPosition, cube);
                    }
                }
            }
        }

        private void QueueOutOfRangeCubes()
        {
            foreach ((Vector3 position, GameObject _) in _CubeColliders.Where(kvp => BoundingBox.Contains(kvp.Key)))
            {
                _DeactivationQueue.Enqueue(position);
            }
        }

        private void ProcessDeactivationQueue()
        {
            while (_DeactivationQueue.Count > 0)
            {
                Vector3 position = _DeactivationQueue.Dequeue();

                GameObject cube = _CubeColliders[position];
                _CubeColliders.Remove(position);
                CubeCache.CacheItem(ref cube);
            }
        }

        private void RecalculateBoundingBox()
        {
            // +1 to include center blocks / position
            int size = (Radius * 2) + 1;

            BoundingBox = new Bounds(_SelfTransform.position, new Vector3(size, size, size));
        }
    }
}