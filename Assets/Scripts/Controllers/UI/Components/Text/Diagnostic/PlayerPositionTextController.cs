#region

using Unity.Mathematics;
using Wyd.Controllers.Entity;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class PlayerPositionTextController : FormattedTextController
    {
        private bool _UpdateTextOnNextFrame;

        private void Start()
        {
            if (PlayerController.Current != default)
            {
                UpdateText(PlayerController.Current.Position, PlayerController.Current.ChunkPosition);

                PlayerController.Current.PositionChanged += (sender, position) => { _UpdateTextOnNextFrame = true; };
                PlayerController.Current.ChunkPositionChanged += (sender, position) =>
                {
                    _UpdateTextOnNextFrame = true;
                };
            }
        }

        private void Update()
        {
            if (!_UpdateTextOnNextFrame)
            {
                return;
            }

            UpdateText(PlayerController.Current.Position, PlayerController.Current.ChunkPosition);

            _UpdateTextOnNextFrame = false;
        }

        private void UpdateText(float3 playerPosition, int3 chunkPosition)
        {
            TextObject.text = string.Format(Format,
                playerPosition.x, playerPosition.y, playerPosition.z,
                chunkPosition.x, chunkPosition.y, chunkPosition.z);
        }
    }
}
