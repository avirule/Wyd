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
            int chunksQueuedForCaching = WorldController.Current.ChunkQueuedForCaching;

            if ((chunksQueuedForBuilding != _LastQueuedForBuildingCount) ||
                (chunksQueuedForCaching != _LastQueuedForCachingCount))
            {
                UpdateChunksQueuedText(chunksQueuedForBuilding, chunksQueuedForCaching);
            }
        }

        private void UpdateChunksQueuedText(int chunksQueuedForBuilding, int chunksQueuedForCaching)
        {
            _LastQueuedForBuildingCount = chunksQueuedForBuilding;
            _LastQueuedForCachingCount = chunksQueuedForCaching;

            _ChunksQueuedText.text = $"Chunks Queued: (b{chunksQueuedForBuilding}, c{chunksQueuedForCaching})";
        }
    }
}