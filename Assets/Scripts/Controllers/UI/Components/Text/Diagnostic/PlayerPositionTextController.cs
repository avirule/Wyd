#region

using Unity.Mathematics;
using Wyd.Controllers.Entity;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class PlayerPositionTextController : UpdatingFormattedTextController
    {
        private bool _TextRequiresUpdate;

        private void Start()
        {
            if (PlayerController.Current != default)
            {
                UpdateText(PlayerController.Current.Position, PlayerController.Current.ChunkPosition);

                PlayerController.Current.PositionChanged += (sender, position) => { _TextRequiresUpdate = true; };
                PlayerController.Current.ChunkPositionChanged += (sender, position) => { _TextRequiresUpdate = true; };
            }
        }

        protected override void TimedUpdate()
        {
            if (!_TextRequiresUpdate)
            {
                return;
            }

            UpdateText(PlayerController.Current.Position, PlayerController.Current.ChunkPosition);

            _TextRequiresUpdate = false;
        }

        private void UpdateText(float3 playerPosition, int3 chunkPosition)
        {
            TextObject.text = string.Format(Format,
                playerPosition.x, playerPosition.y, playerPosition.z,
                chunkPosition.x, chunkPosition.y, chunkPosition.z);
        }
    }
}
