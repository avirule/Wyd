using System;
using UnityEngine;

namespace Environment.Terrain.Generation
{
    public class WorldGenerationSettings : MonoBehaviour
    {
        [NonSerialized] public int Diameter;

        public float Lacunarity;
        public int Octaves;
        public float Persistence;
        public int Radius;
        public float Scale;
        public WorldSeed Seed;
        public string SeedString;

        private void Awake()
        {
            Seed = new WorldSeed(SeedString);
            Diameter = (Radius * 2) + 1;
        }
    }
}