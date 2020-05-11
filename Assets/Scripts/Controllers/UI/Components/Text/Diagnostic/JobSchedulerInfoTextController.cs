#region

using System.Threading.Tasks;
using Wyd.Jobs;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class JobSchedulerInfoTextController : UpdatingFormattedTextController
    {
        private bool _UpdateDiagInfo;

        protected override void Awake()
        {
            base.Awake();

            AsyncJobScheduler.JobQueued += OnJobSchedulerStateChange;
            AsyncJobScheduler.JobStarted += OnJobSchedulerStateChange;
            AsyncJobScheduler.JobFinished += OnJobSchedulerStateChange;
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

        private Task OnJobSchedulerStateChange(object sender, AsyncJobEventArgs args)
        {
            _UpdateDiagInfo = true;

            return Task.CompletedTask;
        }
    }
}
