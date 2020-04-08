#region

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.System.Jobs;
using Object = UnityEngine.Object;

#endregion

namespace Wyd.Controllers.System
{
    public class SystemController : SingletonController<SystemController>
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        public static GameObject TextObject { get; private set; }

        private JobScheduler _JobScheduler;

        public long JobsQueued => _JobScheduler.JobsQueued;
        public long ProcessingJobCount => _JobScheduler.ProcessingJobCount;
        public long WorkerThreadCount => _JobScheduler.WorkerThreadCount;


        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);

            TextObject = LoadResource<GameObject>(@"Prefabs\UI\Components\Text\DiagnosticText");
        }

        private void Start()
        {
            _JobScheduler = new JobScheduler(OptionsController.Current.CPUCoreUtilization);
            _JobScheduler.WorkerCountChanged += OnWorkerCountChanged;
            _JobScheduler.JobQueued += OnJobQueued;
            _JobScheduler.JobStarted += OnJobStarted;
            _JobScheduler.JobFinished += OnJobFinished;

            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.CPUCoreUtilization)))
                {
                    _JobScheduler.ModifyWorkerThreadCount(OptionsController.Current.CPUCoreUtilization);
                }
            };

            _JobScheduler.SpawnWorkers();
        }

        private void OnDestroy()
        {
            _JobScheduler.Abort();
        }

        public async Task<object> QueueAsyncJob(AsyncJob asyncJob)
        {
            return await _JobScheduler.QueueAsyncJob(asyncJob);
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


        #region EVENTS

        public event JobEventHandler JobStarted;
        public event JobEventHandler JobFinished;
        public event EventHandler<long> JobCountChanged;
        public event EventHandler<long> ProcessingJobCountChanged;
        public event EventHandler<long> WorkerThreadCountChanged;

        private void OnWorkerCountChanged(object sender, int args)
        {
            WorkerThreadCountChanged?.Invoke(sender, args);
        }

        private void OnJobQueued(object sender, AsyncJobEventArgs args)
        {
            JobCountChanged?.Invoke(sender, JobsQueued);
        }

        private void OnJobStarted(object sender, AsyncJobEventArgs args)
        {
            JobStarted?.Invoke(sender, args);
            JobCountChanged?.Invoke(sender, JobsQueued);
            ProcessingJobCountChanged?.Invoke(sender, ProcessingJobCount);
        }

        private void OnJobFinished(object sender, AsyncJobEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
            JobCountChanged?.Invoke(sender, JobsQueued);
            ProcessingJobCountChanged?.Invoke(sender, ProcessingJobCount);
        }

        #endregion
    }
}
