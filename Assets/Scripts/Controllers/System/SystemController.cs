#region

using System;
using System.Threading;
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

        private JobScheduler _JobExecutionScheduler;

        public long JobCount => _JobExecutionScheduler.JobCount;
        public long DelegatedJobCount => _JobExecutionScheduler.DelegatedJobCount;
        public long ProcessingJobCount => _JobExecutionScheduler.ProcessingJobCount;
        public long WorkerThreadCount => _JobExecutionScheduler.WorkerThreadCount;


        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);

            //TextObject = GameController.LoadResource<>()
        }

        private void Start()
        {
            _JobExecutionScheduler = new JobScheduler(TimeSpan.FromMilliseconds(5),
                OptionsController.Current.ThreadingMode,
                OptionsController.Current.CPUCoreUtilization);
            _JobExecutionScheduler.WorkerCountChanged += OnWorkerCountChanged;
            _JobExecutionScheduler.JobQueued += OnJobQueued;
            _JobExecutionScheduler.JobStarted += OnJobStarted;
            _JobExecutionScheduler.JobFinished += OnJobFinished;

            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.ThreadingMode)))
                {
                    _JobExecutionScheduler.ThreadingMode = OptionsController.Current.ThreadingMode;
                }
                else if (args.PropertyName.Equals(nameof(OptionsController.Current.CPUCoreUtilization)))
                {
                    _JobExecutionScheduler.ModifyWorkerThreadCount(OptionsController.Current.CPUCoreUtilization);
                }
            };

            _JobExecutionScheduler.Start();
        }

        private void OnDestroy()
        {
            _JobExecutionScheduler.Abort();
        }

        public bool TryQueueJob(Job job, out object identity) => _JobExecutionScheduler.TryQueueJob(job, out identity);


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
        public event EventHandler<long> DelegatedJobCountChanged;
        public event EventHandler<long> ProcessingJobCountChanged;
        public event EventHandler<long> WorkerThreadCountChanged;

        private void OnWorkerCountChanged(object sender, int args)
        {
            WorkerThreadCountChanged?.Invoke(sender, args);
        }

        private void OnJobQueued(object sender, JobEventArgs args)
        {
            JobCountChanged?.Invoke(sender, JobCount);
        }

        private void OnJobDelegated(object sender, JobEventArgs args)
        {
            DelegatedJobCountChanged?.Invoke(sender, DelegatedJobCount);
        }

        private void OnJobStarted(object sender, JobEventArgs args)
        {
            JobStarted?.Invoke(sender, args);
            JobCountChanged?.Invoke(sender, JobCount);
            ProcessingJobCountChanged?.Invoke(sender, ProcessingJobCount);
        }

        private void OnJobFinished(object sender, JobEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
            JobCountChanged?.Invoke(sender, JobCount);
            DelegatedJobCountChanged?.Invoke(sender, DelegatedJobCount);
            ProcessingJobCountChanged?.Invoke(sender, ProcessingJobCount);
        }

        #endregion
    }
}
