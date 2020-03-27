#region

using Wyd.Controllers.System;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class GameStateJobInfoTextController : FormattedTextController
    {
        private bool _UpdateDiagInfo;

        private void Start()
        {
            SystemController.Current.JobCountChanged += (sender, i) => _UpdateDiagInfo = true;
            SystemController.Current.ActiveJobCountChanged += (sender, i) => _UpdateDiagInfo = true;
            SystemController.Current.WorkerThreadCountChanged += (sender, i) => _UpdateDiagInfo = true;
            _UpdateDiagInfo = true;
        }

        private void Update()
        {
            if (_UpdateDiagInfo)
            {
                TextObject.text = string.Format(Format,
                    SystemController.Current.WorkerThreadCount,
                    SystemController.Current.JobCount,
                    SystemController.Current.ActiveJobCount);

                _UpdateDiagInfo = false;
            }
        }
    }
}
