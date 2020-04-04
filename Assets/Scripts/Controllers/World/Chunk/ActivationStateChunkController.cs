#region

using Unity.Mathematics;
using UnityEngine;
using Bounds = Wyd.System.Bounds;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ActivationStateChunkController : MonoBehaviour
    {
        #region INSTANCE MEMBERS

        protected Transform _SelfTransform;
        protected Bounds _Bounds;

        #endregion

        protected virtual void Awake()
        {
            _SelfTransform = transform;
            float3 position = _SelfTransform.position;
            _Bounds.SetMinMaxPoints(position, position + ChunkController.Size);
        }

        protected virtual void OnEnable()
        {
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMaxPoints(position, (float3)position + ChunkController.Size);
        }

        protected virtual void OnDisable()
        {
            StopAllCoroutines();
        }
    }
}
