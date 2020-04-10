#region

using System;
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
                TextObject.text = string.Format(Format,
                    AsyncJobScheduler.AsyncWorkerCount,
                    AsyncJobScheduler.ProcessingJobCount,
                    AsyncJobScheduler.JobsQueued);

                _UpdateDiagInfo = false;
            }
        }
    }
}
