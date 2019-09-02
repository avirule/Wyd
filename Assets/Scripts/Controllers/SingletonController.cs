using Controllers.World;
using UnityEngine;

namespace Controllers
{
    public abstract class SingletonController<T> : MonoBehaviour where T : class
    {
        public static T Current;

        protected virtual void AssignCurrent(T instance)
        {
            if (Current == default)
            {
                Current = instance;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}