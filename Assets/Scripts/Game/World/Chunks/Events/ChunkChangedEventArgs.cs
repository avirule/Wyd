#region

using System.Collections.Generic;
using Unity.Mathematics;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Chunks.Events
{
    public class ChunkChangedEventArgs
    {
        public Volume ChunkVolume { get; }
        public IEnumerable<int3> NeighborDirectionsToUpdate { get; }

        public ChunkChangedEventArgs(Volume chunkVolume, IEnumerable<int3> neighborDirectionsToUpdate)
        {
            ChunkVolume = chunkVolume;
            NeighborDirectionsToUpdate = neighborDirectionsToUpdate;
        }
    }
}
