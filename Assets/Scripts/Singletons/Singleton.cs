#region

using System;
using Serilog;

#endregion

namespace Wyd.Singletons
{
    public static class Singleton
    {
        public static void InstantiateSingleton<T>() where T : Singleton<T>, new() => new T();
    }

    public class Singleton<T>
    {
        private static Singleton<T> _SingletonInstance;
        private static T _Instance;

        public static T Instance
        {
            get
            {
                Validate();

                return _Instance;
            }
        }

        private static void Validate()
        {
            if (!(_Instance is object))
            {
                throw new InvalidOperationException($"Singleton '{typeof(T)}' has not been instantiated.");
            }
        }

        protected void AssignSingletonInstance(T instance)
        {
            if ((_SingletonInstance != default) && (_SingletonInstance != this))
            {
                throw new ArgumentException($"Singleton for type {typeof(T)} already exists.", nameof(instance));
            }
            else
            {
                _SingletonInstance = this;
                _Instance = instance;

                Log.Information($"Singleton '{typeof(T)}' has been instantiated.");
            }
        }
    }
}
