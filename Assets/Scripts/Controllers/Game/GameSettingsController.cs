#region

using UnityEngine;

#endregion

namespace Controllers.Game
{
    public class GameSettingsController : MonoBehaviour
    {
        public int MaximumChunkCacheSize;
        public int MaximumChunkLoadTimeCacheSize;
        public int MaximumFrameRateCacheSize;
        public int ShadowRadius;
        public int ExpensiveMeshingRadius;
        public float MaximumInternalFrameTime;
    }
}