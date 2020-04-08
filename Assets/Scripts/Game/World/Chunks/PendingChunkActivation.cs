using Unity.Mathematics;

namespace Wyd.Game.World.Chunks
{
    public class PendingChunkActivation
    {
        public float3 Origin { get; }
        public float3 LoaderDifference { get; }

        public PendingChunkActivation(float3 origin, float3 loaderDifference)
        {
            Origin = origin;
            LoaderDifference = loaderDifference;
        }
    }
}
