#region

using Controllers;

#endregion

namespace Extensions
{
    public abstract class LockableSingletonController<T> : SingletonController<T> where T : class
    {
        protected object KeyMaster;
        protected bool Locked;

        public virtual bool IsLockedFor(object keyMaster) => !Locked || (KeyMaster == keyMaster);

        public virtual bool Lock(object keyMaster)
        {
            if (Locked)
            {
                return KeyMaster == keyMaster;
            }

            KeyMaster = keyMaster;
            Locked = true;
            return true;
        }

        public virtual bool Unlock(object keyMaster)
        {
            if (!Locked)
            {
                return KeyMaster == keyMaster;
            }

            KeyMaster = null;
            Locked = false;
            return true;
        }
    }
}
