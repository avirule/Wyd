namespace Environment.Terrain.Generation
{
    public struct WorldGenSettings
    {
        /// <summary>
        ///     Define number of iterations
        /// </summary>
        public int Octaves;

        /// <summary>
        ///     Adjusts scale of noise map
        /// </summary>
        public float Scale;

        /// <summary>
        ///     Usually a value between 0 and 1
        /// </summary>
        public float Persistence;

        /// <summary>
        ///     Usually a value above 1
        /// </summary>
        public float Lacunarity;

        public WorldGenSettings(int octaves, float scale, float persistence, float lacunarity)
        {
            Octaves = octaves;
            Scale = scale;
            Persistence = persistence;
            Lacunarity = lacunarity;
        }
    }
}