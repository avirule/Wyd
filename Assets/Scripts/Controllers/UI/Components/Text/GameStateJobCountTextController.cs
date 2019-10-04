#region

using Controllers.State;

#endregion

namespace Controllers.UI.Components.Text
{
    public class GameStateJobCountTextController : FormattedTextController
    {
        private int _NewJobCount;

        private void Start()
        {
            GameController.Current.JobCountChanged += (sender, i) => { _NewJobCount = i; };
            _NewJobCount = GameController.Current.JobCount;
        }

        private void Update()
        {
            if (_NewJobCount != -1)
            {
                TextObject.text = string.Format(Format, _NewJobCount);
                _NewJobCount = -1;
            }
        }
    }
}
