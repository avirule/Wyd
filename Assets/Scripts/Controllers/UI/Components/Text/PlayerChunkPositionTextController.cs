#region

using Controllers.Entity;

#endregion

namespace Controllers.UI.Components.Text
{
    public class PlayerChunkPositionTextController : FormattedTextController
    {
        private void Start()
        {
            if (PlayerController.Current != default)
            {
                PlayerController.Current.ChunkPositionChanged += (sender, position) =>
                {
                    _TextObject.text = string.Format(_Format, position.x, position.y, position.z);
                };
            }
        }
    }
}
