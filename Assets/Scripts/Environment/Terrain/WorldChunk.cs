using Static;
using UnityEngine;

namespace Environment.Terrain
{
    public class WorldChunk
    {
        public readonly Chunk Chunk;
        public readonly GameObject GameObject;
        public Vector3Int Position;

        public WorldChunk(GameObject gameObject)
        {
            GameObject = gameObject;
            Chunk = gameObject.GetComponent<Chunk>();
            Chunk.Position = Position = GameObject.transform.position.ToInt();
        }
    }
}