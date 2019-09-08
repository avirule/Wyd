#region

using System.Collections.Generic;
using Controllers.Entity;
using Game;
using Game.Entity;
using UnityEngine;

#endregion

namespace Controllers.World
{
    public class CollisionTokenController : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        private GameObject _CollisionTokenObject;
        private List<CollisionToken> _CollisionTokens;
        private Mesh _CombinedColliderMesh;
        private MeshCollider _MeshCollider;
        private bool _UpdateColliderMesh;

        public bool PrimaryLoaderChangedChunk { get; set; }

        private void Awake()
        {
            _CollisionTokenObject = Resources.Load<GameObject>(@"Prefabs/CollisionToken");
            _CollisionTokens = new List<CollisionToken>();
            _CombinedColliderMesh = new Mesh();
            _MeshCollider = GetComponent<MeshCollider>();
        }

        private void Start()
        {
            PlayerController.Current.RegisterEntityChangedSubscriber(this);
        }

        private void OnApplicationQuit()
        {
            Destroy(_CombinedColliderMesh);
        }

        public void RegisterEntity(Transform attachTo, int loadRadius)
        {
            GameObject entityToken = Instantiate(_CollisionTokenObject, transform);
            CollisionToken collisionToken = entityToken.GetComponent<CollisionToken>();
            collisionToken.AttachedTransform = attachTo;
            collisionToken.Radius = loadRadius;
            collisionToken.UpdatedMesh += (sender, mesh) => { _UpdateColliderMesh = true; };

            _CollisionTokens.Add(collisionToken);
        }

        private void GenerateColliderMesh()
        {
            List<CombineInstance> combines = new List<CombineInstance>();

            foreach (CollisionToken collisionToken in _CollisionTokens)
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