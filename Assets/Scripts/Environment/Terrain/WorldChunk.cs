using UnityEngine;

namespace Environment.Terrain
{
    public struct WorldChunk
    {
        public readonly GameObject ChunkObject;
        public readonly Chunk Chunk;
        public Vector3Int Position => Chunk.Position;

        public WorldChunk(GameObject chunkObject)
        {
            ChunkObject = chunkObject;

            Chunk = ChunkObject.GetComponent<Chunk>();
        }
    }
}