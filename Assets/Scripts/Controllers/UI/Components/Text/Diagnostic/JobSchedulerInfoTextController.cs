#region

using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class JobSchedulerInfoTextController : UpdatingFormattedTextController
    {
        private bool _UpdateDiagInfo;

        protected override void Awake()
        {
            base.Awake();

            AsyncJobScheduler.JobQueued += (sender, args) => _UpdateDiagInfo = true;
            AsyncJobScheduler.JobStarted += (sender, args) => _UpdateDiagInfo = true;
            AsyncJobScheduler.JobFinished += (sender, args) => _UpdateDiagInfo = true;
            _UpdateDiagInfo = true;
        }

        protected override void TimedUpdate()
        {
            if (_UpdateDiagInfo)
            {
                _TextObject.text = string.Format(_Format,
                    AsyncJobScheduler.MaximumProcessingJobs,
                    AsyncJobScheduler.ProcessingJobs,
                    AsyncJobScheduler.QueuedJobs);

                _UpdateDiagInfo = false;
            }
        }
    }
}
