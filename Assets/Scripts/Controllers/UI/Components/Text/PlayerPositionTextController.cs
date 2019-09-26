#region

using Controllers.Entity;

#endregion

namespace Controllers.UI.Components.Text
{
    public class PlayerPositionTextController : FormattedTextController
    {
        private void Start()
        {
            if (PlayerController.Current != default)
            {
                PlayerController.Current.PositionChanged += (sender, position) =>
                {
                    _TextObject.text = string.Format(_Format, position.x, position.y, position.z);
                };
            }
        }
    }
}
