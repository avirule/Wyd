#region

using Controllers.Entity;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class PlayerPositionTextController : FormattedTextController
    {
        private void Start()
        {
            if (PlayerController.Current != default)
            {
                UpdateText(PlayerController.Current.Position);

                PlayerController.Current.PositionChanged += (sender, position) => { UpdateText(position); };
            }
        }

        private void UpdateText(Vector3 position)
        {
            _TextObject.text = string.Format(_Format, position.x, position.y, position.z);
        }
    }
}
