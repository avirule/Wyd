#region

using UnityEngine;
using Wyd.Controllers.Entity;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class PlayerChunkPositionTextController : FormattedTextController
    {
        private void Start()
        {
            if (PlayerController.Current != default)
            {
                UpdateText(PlayerController.Current.Position);

                PlayerController.Current.ChunkPositionChanged += (sender, position) => { UpdateText(position); };
            }
        }

        private void UpdateText(Vector3 position)
        {
            TextObject.text = string.Format(Format, position.x, position.y, position.z);
        }
    }
}
