#region

using UnityEngine;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ActivationStateChunkController : MonoBehaviour
    {
        #region INSTANCE MEMBERS

        protected Transform _SelfTransform;
        protected Bounds _Bounds;

        #endregion

        public ActivationStateChunkController() { }

        public ActivationStateChunkController(Bounds bounds) => _Bounds = bounds;

        protected virtual void Awake()
        {
            _SelfTransform = transform;
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMax(position, position + ChunkController.Size);
        }

        public virtual void Activate(Vector3 position, bool setPosition)
        {
            if (setPosition)
            {
                _SelfTransform.position = position;
            }

            _Bounds.SetMinMax(position, position + ChunkController.Size);
        }

        public virtual void Deactivate()
        {
            StopAllCoroutines();
        }
    }
}
