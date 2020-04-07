#region

using Wyd.Controllers.System;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class JobSchedulerInfoTextController : FormattedTextController
    {
        private bool _UpdateDiagInfo;

        private void Start()
        {
            SystemController.Current.JobCountChanged += (sender, args) => _UpdateDiagInfo = true;
            SystemController.Current.DelegatedJobCountChanged += (sender, args) => _UpdateDiagInfo = true;
            SystemController.Current.ProcessingJobCountChanged += (sender, args) => _UpdateDiagInfo = true;
            SystemController.Current.WorkerThreadCountChanged += (sender, args) => _UpdateDiagInfo = true;
            _UpdateDiagInfo = true;
        }

        private void Update()
        {
            if (_UpdateDiagInfo)
            {
                TextObject.text = string.Format(Format,
                    SystemController.Current.WorkerThreadCount,
                    SystemController.Current.JobCount,
                    SystemController.Current.DelegatedJobCount,
                    SystemController.Current.ProcessingJobCount);

                _UpdateDiagInfo = false;
            }
        }
    }
}
