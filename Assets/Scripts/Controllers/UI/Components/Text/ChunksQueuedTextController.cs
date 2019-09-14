#region

using Controllers.World;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunksQueuedTextController : MonoBehaviour
    {
        private string _Format;
        private TextMeshProUGUI _ChunksQueuedText;
        private int _LastQueuedForBuildingCount;

        private void Awake()
        {
            _ChunksQueuedText = GetComponent<TextMeshProUGUI>();
            _Format = _ChunksQueuedText.text;
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

            _ChunksQueuedText.text = string.Format(_Format, _LastQueuedForBuildingCount);
        }
    }
}
