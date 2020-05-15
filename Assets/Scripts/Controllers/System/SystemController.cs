#region

using System.Threading;
using UnityEngine;
using Wyd.Jobs;
using Wyd.Singletons;

#endregion

namespace Wyd.Controllers.System
{
    public class SystemController : SingletonController<SystemController>
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        private static GameObject TextObject { get; set; }

        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);

            TextObject = Resources.Load<GameObject>(@"Prefabs\UI\Components\Text\DiagnosticText");

            Singleton.InstantiateSingleton<Options>();
            Singleton.InstantiateSingleton<Singletons.Diagnostics>();
        }

        private void OnDestroy()
        {
            AsyncJobScheduler.Abort(true);
        }
    }
}
