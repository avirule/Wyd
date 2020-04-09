#region

using Unity.Mathematics;
using UnityEngine;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ActivationStateChunkController : MonoBehaviour
    {
        #region INSTANCE MEMBERS

        protected Transform _SelfTransform;

        public float3 OriginPoint { get; private set; }

        #endregion

        protected virtual void Awake()
        {
            _SelfTransform = transform;
        }

        protected virtual void OnEnable()
        {
            OriginPoint = _SelfTransform.position;
        }

        protected virtual void OnDisable()
        {
            StopAllCoroutines();
        }
    }
}
