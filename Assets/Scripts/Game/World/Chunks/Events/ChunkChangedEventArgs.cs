#region

using System.Collections.Generic;
using Unity.Mathematics;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Chunks.Events
{
    public class ChunkChangedEventArgs
    {
        public Bounds ChunkBounds { get; }
        public IEnumerable<int3> NeighborDirectionsToUpdate { get; }

        public ChunkChangedEventArgs(Bounds chunkBounds, IEnumerable<int3> neighborDirectionsToUpdate)
        {
            ChunkBounds = chunkBounds;
            NeighborDirectionsToUpdate = neighborDirectionsToUpdate;
        }
    }
}
