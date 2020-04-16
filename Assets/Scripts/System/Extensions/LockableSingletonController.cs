#region

using Wyd.Controllers;

#endregion

namespace Wyd.System.Extensions
{
    public abstract class LockableSingletonController<T> : SingletonController<T> where T : class
    {
        protected object _KeyMaster;
        protected bool _Locked;

        public virtual bool IsLockedFor(object keyMaster) => !_Locked || (_KeyMaster == keyMaster);

        public virtual bool Lock(object keyMaster)
        {
            if (_Locked)
            {
                return _KeyMaster == keyMaster;
            }

            _KeyMaster = keyMaster;
            _Locked = true;
            return true;
        }

        public virtual bool Unlock(object keyMaster)
        {
            if (!_Locked)
            {
                return _KeyMaster == keyMaster;
            }

            _KeyMaster = null;
            _Locked = false;
            return true;
        }
    }
}
