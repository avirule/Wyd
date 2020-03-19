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
        protected Vector3 _Position;

        #endregion

        public ActivationStateChunkController() { }

        public ActivationStateChunkController(Bounds bounds) => _Bounds = bounds;

        protected virtual void Awake()
        {
            _SelfTransform = transform;
            _Position = _SelfTransform.position;
            _Bounds.SetMinMax(_Position, _Position + ChunkController.Size);
        }

        public virtual void Activate(Vector3 position, bool setPosition)
        {
            if (setPosition)
            {
                _SelfTransform.position = _Position = position;
            }

            _Bounds.SetMinMax(_Position, _Position + ChunkController.Size);
        }

        public virtual void Deactivate()
        {
            StopAllCoroutines();
        }
    }
}
