#region

using System.Threading;
using UnityEngine;

#endregion

namespace Wyd.Controllers.System
{
    public class SystemController : SingletonController<SystemController>
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        public static GameObject TextObject { get; private set; }

        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);

            TextObject = LoadResource<GameObject>(@"Prefabs\UI\Components\Text\DiagnosticText");
        }


        public static T LoadResource<T>(string path) where T : Object
        {
            T resource = Resources.Load<T>(path);
            return resource;
        }

        public static T[] LoadAllResources<T>(string path) where T : Object
        {
            T[] resources = Resources.LoadAll<T>(path);
            return resources;
        }
    }
}
