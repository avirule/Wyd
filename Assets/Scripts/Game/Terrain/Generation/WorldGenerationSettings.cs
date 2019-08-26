#region

using UnityEngine;

#endregion

namespace Game.Terrain.Generation
{
    public class WorldGenerationSettings : MonoBehaviour
    {
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
            Seed = new WorldSeed(SeedString);
        }
    }
}