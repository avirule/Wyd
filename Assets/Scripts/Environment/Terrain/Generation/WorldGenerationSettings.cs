#region

using UnityEngine;

#endregion

namespace Environment.Terrain.Generation
{
    public class WorldGenerationSettings : MonoBehaviour
    {
        public Vector3Int ChunkSize;
        public float Lacunarity;
        public int Octaves;
        public float Persistence;
        public int Radius;
        public float Scale;
        public WorldSeed Seed;
        public string SeedString;
        public int Diameter => (Radius * 2) + 1;

        private void Awake()
        {
            Chunk.Size = ChunkSize;
            Seed = new WorldSeed(SeedString);
        }
    }
}