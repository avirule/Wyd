#region

using UnityEngine;

#endregion

namespace Wyd.Controllers
{
    public abstract class SingletonController<T> : MonoBehaviour where T : class
    {
        private static SingletonController<T> _singletonInstance;

        public static T Current { get; private set; }

        protected virtual void AssignSingletonInstance(T instance)
        {
            if ((_singletonInstance != default) && (_singletonInstance != this))
            {
                Destroy(gameObject);
            }
            else
            {
                _singletonInstance = this;
                Current = instance;
            }
        }
    }
}
