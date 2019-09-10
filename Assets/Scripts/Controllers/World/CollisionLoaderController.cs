#region

using System.Collections.Generic;
using Controllers.Entity;
using Game;
using Game.Entity;
using UnityEngine;

#endregion

namespace Controllers.World
{
    [RequireComponent(typeof(MeshCollider))]
    public class CollisionLoaderController : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        private List<CollisionLoader> _CollisionLoaders;
        private Mesh _CombinedColliderMesh;
        private MeshCollider _MeshCollider;
        private bool _UpdateColliderMesh;

        public GameObject CollisionLoaderObject;
        
        public bool PrimaryLoaderChangedChunk { get; set; }

        private void Awake()
        {
            _CollisionLoaders = new List<CollisionLoader>();
            _CombinedColliderMesh = new Mesh();
            _MeshCollider = GetComponent<MeshCollider>();
        }

        private void Start()
        {
            PlayerController.Current.RegisterEntityChangedSubscriber(this);
        }

        private void Update()
        {
            if (_UpdateColliderMesh)
            {
                GenerateColliderMesh();
            }
        }

        private void OnApplicationQuit()
        {
            Destroy(_CombinedColliderMesh);
        }

        public void RegisterEntity(Transform attachTo, int loadRadius)
        {
            GameObject entityToken = Instantiate(CollisionLoaderObject, transform);
            CollisionLoader collisionLoader = entityToken.GetComponent<CollisionLoader>();
            collisionLoader.AttachedTransform = attachTo;
            collisionLoader.Radius = loadRadius;
            collisionLoader.UpdatedMesh += (sender, mesh) => { _UpdateColliderMesh = true; };

            _CollisionLoaders.Add(collisionLoader);
        }

        private void GenerateColliderMesh()
        {
            List<CombineInstance> combines = new List<CombineInstance>();

            foreach (CollisionLoader collisionToken in _CollisionLoaders)
            {
                if ((collisionToken.Mesh == default) || (collisionToken.Mesh.vertexCount == 0))
                {
                    continue;
                }

                CombineInstance combine = new CombineInstance
                {
                    mesh = collisionToken.Mesh,
                    transform = collisionToken.transform.localToWorldMatrix
                };

                combines.Add(combine);
            }

            _CombinedColliderMesh.CombineMeshes(combines.ToArray(), true, true);
            _CombinedColliderMesh.Optimize();

            _MeshCollider.sharedMesh = _CombinedColliderMesh;

            _UpdateColliderMesh = false;
        }
    }
}
