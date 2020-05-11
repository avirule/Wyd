#region

using System.Collections.Generic;
using Unity.Mathematics;

#endregion

namespace Wyd.World.Chunks
{
    public class ChunkChangedEventArgs
    {
        public float3 OriginPoint { get; }
        public IEnumerable<float3> NeighborDirectionsToUpdate { get; }

        public ChunkChangedEventArgs(float3 originPoint, IEnumerable<float3> neighborDirectionsToUpdate)
        {
            OriginPoint = originPoint;
            NeighborDirectionsToUpdate = neighborDirectionsToUpdate;
        }
    }
}
