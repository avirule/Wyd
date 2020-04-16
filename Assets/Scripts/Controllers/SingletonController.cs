#region

using UnityEngine;

#endregion

namespace Wyd.Controllers
{
    public abstract class SingletonController<T> : MonoBehaviour where T : class
    {
        private static SingletonController<T> _SingletonInstance;

        public static T Current { get; private set; }

        protected virtual void AssignSingletonInstance(T instance)
        {
            if ((_SingletonInstance != default) && (_SingletonInstance != this))
            {
                Destroy(gameObject);
            }
            else
            {
                _SingletonInstance = this;
                Current = instance;
            }
        }
    }
}
