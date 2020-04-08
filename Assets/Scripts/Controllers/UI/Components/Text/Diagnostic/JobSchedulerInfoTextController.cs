#region

using System;
using Wyd.Controllers.System;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class JobSchedulerInfoTextController : FormattedTextController
    {
        private bool _UpdateDiagInfo;

        private void Start()
        {
            JobScheduler.JobQueued += (sender, args) => _UpdateDiagInfo = true;
            JobScheduler.JobStarted += (sender, args) => _UpdateDiagInfo = true;
            JobScheduler.JobFinished += (sender, args) => _UpdateDiagInfo = true;
            _UpdateDiagInfo = true;
        }

        private void Update()
        {
            if (_UpdateDiagInfo)
            {
                TextObject.text = string.Format(Format, JobScheduler.WorkerThreadCount,JobScheduler.ProcessingJobCount,
                    JobScheduler.JobsQueued);

                _UpdateDiagInfo = false;
            }
        }
    }
}
