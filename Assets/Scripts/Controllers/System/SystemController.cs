#region

using System.Threading;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.System.Jobs;

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

            TextObject = Resources.Load<GameObject>(@"Prefabs\UI\Components\Text\DiagnosticText");
        }

        private void Start()
        {
            AsyncJobScheduler.ModifyMaximumProcessingJobCount(OptionsController.Current.AsyncWorkerCount);
        }

        private void OnDestroy()
        {
            AsyncJobScheduler.Abort(true);
        }
    }
}
