#region

using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class JobSchedulerInfoTextController : FormattedTextController
    {
        private bool _UpdateDiagInfo;

        private void Start()
        {
            AsyncJobScheduler.JobQueued += (sender, args) => _UpdateDiagInfo = true;
            AsyncJobScheduler.JobStarted += (sender, args) => _UpdateDiagInfo = true;
            AsyncJobScheduler.JobFinished += (sender, args) => _UpdateDiagInfo = true;
            _UpdateDiagInfo = true;
        }

        private void Update()
        {
            if (_UpdateDiagInfo)
            {
                TextObject.text = string.Format(Format, AsyncJobScheduler.WorkerThreadCount,
                    AsyncJobScheduler.ProcessingJobCount,
                    AsyncJobScheduler.JobsQueued);

                _UpdateDiagInfo = false;
            }
        }
    }
}
