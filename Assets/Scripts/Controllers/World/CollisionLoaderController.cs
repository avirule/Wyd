#region

using System.Collections.Generic;
using UnityEngine;
using Wyd.Game;

#endregion

namespace Wyd.Controllers.World
{
    [RequireComponent(typeof(MeshCollider))]
    public class CollisionLoaderController : MonoBehaviour
    {
        private List<CollisionLoader> _CollisionLoaders;
        private Mesh _CombinedColliderMesh;
        private MeshCollider _MeshCollider;
        private bool _UpdateColliderMesh;

        public GameObject CollisionLoaderObject;

        private void Awake()
        {
            _CollisionLoaders = new List<CollisionLoader>();
            _CombinedColliderMesh = new Mesh();
            _MeshCollider = GetComponent<MeshCollider>();
        }

        private void Update()
        {
            // if (_UpdateColliderMesh)
            // {
            //     GenerateColliderMesh();
            // }
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
            //collisionLoader.UpdatedMesh += (sender, mesh) => { _UpdateColliderMesh = true; };

            _CollisionLoaders.Add(collisionLoader);
        }

        // private void GenerateColliderMesh()
        // {
        //     List<CombineInstance> combines = new List<CombineInstance>();
        //
        //     foreach (CollisionLoader collisionToken in _CollisionLoaders.Where(loader =>
        //         (loader.Mesh != default) && (loader.Mesh.vertexCount > 0)))
        //     {
        //         CombineInstance combine = new CombineInstance
        //         {
        //             mesh = collisionToken.Mesh,
        //             transform = collisionToken.transform.localToWorldMatrix
        //         };
        //
        //         combines.Add(combine);
        //     }
        //
        //     _CombinedColliderMesh.CombineMeshes(combines.ToArray(), true, true);
        //     _CombinedColliderMesh.Optimize();
        //
        //     _MeshCollider.sharedMesh = _CombinedColliderMesh;
        //
        //     _UpdateColliderMesh = false;
        // }
    }
}
