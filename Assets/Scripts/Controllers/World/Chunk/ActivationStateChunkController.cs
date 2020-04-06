#region

using Unity.Mathematics;
using UnityEngine;
using Wyd.System;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ActivationStateChunkController : MonoBehaviour
    {
        #region INSTANCE MEMBERS

        protected Transform _SelfTransform;
        protected Volume _Volume;

        #endregion

        protected virtual void Awake()
        {
            _SelfTransform = transform;
        }

        protected virtual void OnEnable()
        {
            float3 position = _SelfTransform.position;
            _Volume.SetMinMaxPoints(position, position + ChunkController.Size);
        }

        protected virtual void OnDisable()
        {
            StopAllCoroutines();
        }
    }
}
