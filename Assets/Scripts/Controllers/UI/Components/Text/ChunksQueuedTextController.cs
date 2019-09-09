#region

using Controllers.World;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunksQueuedTextController : MonoBehaviour
    {
        private TextMeshProUGUI _ChunksQueuedText;
        private int _LastQueuedForBuildingCount;
        private int _LastQueuedForCachingCount;

        private void Awake()
        {
            _ChunksQueuedText = GetComponent<TextMeshProUGUI>();
            _ChunksQueuedText.text = "Chunks Queued: (b0, c0)";
        }

        private void Update()
        {
            int chunksQueuedForBuilding = WorldController.Current.ChunksQueuedForBuilding;

            if (chunksQueuedForBuilding != _LastQueuedForBuildingCount)
            {
                UpdateChunksQueuedText(chunksQueuedForBuilding);
            }
        }

        private void UpdateChunksQueuedText(int chunksQueuedForBuilding)
        {
            _LastQueuedForBuildingCount = chunksQueuedForBuilding;

            _ChunksQueuedText.text = $"Chunks Queued: (b{chunksQueuedForBuilding})";
        }
    }
}
