using UnityEngine;

namespace Game.World.Chunks
{
    public class ChunkChangedEventArgs
    {
        public Bounds ChunkBounds { get; }
        public bool ShouldUpdateNeighbors { get; }

        public ChunkChangedEventArgs(Bounds chunkBounds, bool shouldUpdateNeighbors)
        {
            ChunkBounds = chunkBounds;
            ShouldUpdateNeighbors = shouldUpdateNeighbors;
        }
    }
}
