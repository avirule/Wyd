#region

using Controllers.World;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ChunksLoadedTextController : MonoBehaviour
    {
        private string _Format;
        private TextMeshProUGUI _ChunksLoadedText;
        private int _LastChunksActiveCount;
        private int _LastChunksCachedCount;

        private void Awake()
        {
            _ChunksLoadedText = GetComponent<TextMeshProUGUI>();
            _Format = _ChunksLoadedText.text;
        }

        private void Start()
        {
            int chunksActive = WorldController.Current.ChunksActiveCount;
            int chunksCached = WorldController.Current.ChunksCachedCount;

            UpdateChunksLoadedText(chunksActive, chunksCached);
        }

        private void Update()
        {
            int chunksActive = WorldController.Current.ChunksActiveCount;
            int chunksCached = WorldController.Current.ChunksCachedCount;

            if ((chunksActive != _LastChunksActiveCount) || (chunksCached != _LastChunksCachedCount))
            {
                UpdateChunksLoadedText(chunksActive, chunksCached);
            }
        }

        private void UpdateChunksLoadedText(int chunksActive, int chunksCached)
        {
            _LastChunksActiveCount = chunksActive;
            _LastChunksCachedCount = chunksCached;

            _ChunksLoadedText.text = string.Format(_Format, _LastChunksActiveCount, _LastChunksCachedCount);
        }
    }
}
