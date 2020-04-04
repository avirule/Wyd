#region

using Unity.Mathematics;
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
                UpdateText(PlayerController.Current.ChunkPosition);

                PlayerController.Current.ChunkPositionChanged += (sender, position) => { UpdateText(position); };
            }
        }

        private void UpdateText(int3 position)
        {
            TextObject.text = string.Format(Format, position.x, position.y, position.z);
        }
    }
}
