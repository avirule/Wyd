#region

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Wyd.Controllers.State;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.System
{
    public class SystemController : SingletonController<SystemController>
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        private Stopwatch _FrameTimer;
        private JobScheduler _JobExecutionScheduler;

        public long JobCount => _JobExecutionScheduler.JobCount;
        public long ActiveJobCount => _JobExecutionScheduler.ProcessingJobCount;
        public long WorkerThreadCount => _JobExecutionScheduler.WorkerThreadCount;

        public bool IsInSafeFrameTime() => _FrameTimer.Elapsed <= OptionsController.Current.MaximumInternalFrameTime;

        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);

            _FrameTimer = new Stopwatch();
        }

        private void Start()
        {
            _JobExecutionScheduler = new JobScheduler(TimeSpan.FromMilliseconds(200),
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

        private void Update()
        {
            _FrameTimer.Restart();
        }

        private void OnDestroy()
        {
            _JobExecutionScheduler.Abort();
        }

        public bool TryQueueJob(Job job, out object identity) => _JobExecutionScheduler.TryQueueJob(job, out identity);

        #region EVENTS

        public event JobEventHandler JobStarted;
        public event JobEventHandler JobFinished;
        public event EventHandler<long> JobCountChanged;
        public event EventHandler<long> ActiveJobCountChanged;
        public event EventHandler<long> WorkerThreadCountChanged;

        private void OnWorkerCountChanged(object sender, int args)
        {
            WorkerThreadCountChanged?.Invoke(sender, args);
        }

        private void OnJobQueued(object sender, JobEventArgs args)
        {
            JobCountChanged?.Invoke(sender, _JobExecutionScheduler.JobCount);
        }

        private void OnJobStarted(object sender, JobEventArgs args)
        {
            JobStarted?.Invoke(sender, args);
            JobCountChanged?.Invoke(sender, _JobExecutionScheduler.JobCount);
            ActiveJobCountChanged?.Invoke(sender, _JobExecutionScheduler.ProcessingJobCount);
        }

        private void OnJobFinished(object sender, JobEventArgs args)
        {
            JobFinished?.Invoke(sender, args);
            JobCountChanged?.Invoke(sender, _JobExecutionScheduler.JobCount);
            ActiveJobCountChanged?.Invoke(sender, _JobExecutionScheduler.ProcessingJobCount);
        }

        #endregion
    }
}
