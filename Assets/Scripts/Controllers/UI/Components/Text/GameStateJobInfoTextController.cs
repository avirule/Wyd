#region

using Wyd.Controllers.State;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class GameStateJobInfoTextController : FormattedTextController
    {
        private bool _UpdateDiagInfo;

        private void Start()
        {
            GameController.Current.JobCountChanged += (sender, i) => _UpdateDiagInfo = true;
            GameController.Current.ActiveJobCountChanged += (sender, i) => _UpdateDiagInfo = true;
            GameController.Current.WorkerThreadCountChanged += (sender, i) => _UpdateDiagInfo = true;
            _UpdateDiagInfo = true;
        }

        private void Update()
        {
            if (_UpdateDiagInfo)
            {
                TextObject.text = string.Format(Format,
                    GameController.Current.WorkerThreadCount,
                    GameController.Current.JobCount,
                    GameController.Current.ActiveJobCount);

                _UpdateDiagInfo = false;
            }
        }
    }
}
