#region

using System.Collections.Generic;
using Unity.Mathematics;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Chunks.Events
{
    public class ChunkChangedEventArgs
    {
        public float3 OriginPoint { get; }
        public IEnumerable<int3> NeighborDirectionsToUpdate { get; }

        public ChunkChangedEventArgs(float3 originPoint, IEnumerable<int3> neighborDirectionsToUpdate)
        {
            OriginPoint = originPoint;
            NeighborDirectionsToUpdate = neighborDirectionsToUpdate;
        }
    }
}
