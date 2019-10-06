#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkChangedEventArgs
    {
        public Bounds ChunkBounds { get; }
        public IEnumerable<Vector3> NeighborDirectionsToUpdate { get; }

        public ChunkChangedEventArgs(Bounds chunkBounds, IEnumerable<Vector3> neighborDirectionsToUpdate)
        {
            ChunkBounds = chunkBounds;
            NeighborDirectionsToUpdate = neighborDirectionsToUpdate;
        }
    }
}
