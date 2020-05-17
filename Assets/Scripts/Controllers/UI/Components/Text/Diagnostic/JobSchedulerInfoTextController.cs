#region

using ConcurrentAsyncScheduler;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class JobSchedulerInfoTextController : UpdatingFormattedTextController
    {
        private bool _UpdateDiagInfo;

        protected override void OnEnable()
        {
            base.OnEnable();

            AsyncJobScheduler.JobQueued += OnJobSchedulerStateChange;
            AsyncJobScheduler.JobStarted += OnJobSchedulerStateChange;
            AsyncJobScheduler.JobFinished += OnJobSchedulerStateChange;

            _UpdateDiagInfo = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            AsyncJobScheduler.JobQueued -= OnJobSchedulerStateChange;
            AsyncJobScheduler.JobStarted -= OnJobSchedulerStateChange;
            AsyncJobScheduler.JobFinished -= OnJobSchedulerStateChange;
        }

        protected override void TimedUpdate()
        {
            if (_UpdateDiagInfo)
            {
                _TextObject.text = string.Format(_Format, AsyncJobScheduler.MaximumConcurrentJobs, AsyncJobScheduler.ProcessingJobsCount,
                    AsyncJobScheduler.QueuedJobsCount);

                _UpdateDiagInfo = false;
            }
        }

        private void OnJobSchedulerStateChange(object _, AsyncJob __)
        {
            _UpdateDiagInfo = true;
        }
    }
}
