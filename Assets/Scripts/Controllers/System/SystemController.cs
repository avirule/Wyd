#region

using System.Threading;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Jobs;

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
        }

        private void OnDestroy()
        {
            AsyncJobScheduler.Abort(true);
        }
    }
}
