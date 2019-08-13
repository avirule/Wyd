#region

using UnityEngine;

#endregion

namespace Controllers.Game
{
    public enum CacheCullingAggression
    {
        /// <summary>
        ///     Active cache culling will force the game to keep
        ///     the total amount of cached chunks at or below
        ///     the maximum
        /// </summary>
        Active,
        /// <summary>
        ///     Passive culling will only cull chunks when
        ///     given enough processing time to do so.
        /// </summary>
        Passive
    }
    
    public class GameSettingsController : MonoBehaviour
    {
        public int MaximumChunkCacheSize;
        public CacheCullingAggression CacheCullingAggression;
        public int MaximumChunkLoadTimeCacheSize;
        public int MaximumFrameRateCacheSize;
        public int ShadowRadius;
        public int ExpensiveMeshingRadius;
        public float MaximumInternalFrameTime;
    }
}