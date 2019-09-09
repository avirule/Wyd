#region

using Game.World;
using Logging;
using NLog;
using Noise.Perlin;
using UnityEngine;

#endregion

namespace Noise
{
    public class NoiseMap
    {
        public BoundsInt Bounds;
        public float[][] Map;
        public bool Ready;

        /// <summary>
        ///     Initialises a new instance of the <see cref="Noise.NoiseMap" /> class.
        /// </summary>
        /// <param name="map"><see cref="T:float[][]" /> map of noise heights.</param>
        /// <param name="center"><see cref="UnityEngine.Vector3Int" /> center position of the map.</param>
        /// <param name="size"><see cref="UnityEngine.Vector3Int" /> size of the section, using X and Z.</param>
        public NoiseMap(float[][] map, Vector3Int center, Vector3Int size)
        {
            Bounds = new BoundsInt(center - new Vector3Int(size.x / 2, 0, size.z / 2), size);
            InitMap(map);

            Ready = false;
        }

        private void InitMap(float[][] map)
        {
            Map = map ?? new float[Bounds.size.x][];

            if (map == default)
            {
                for (int x = 0; x < Bounds.size.x; x++)
                {
                    Map[x] = new float[Bounds.size.z];
                }
            }
        }

        /// <summary>
        ///     Generates or regenerates a noise map and adjusts map bounds.
        /// </summary>
        /// <param name="offset"><see cref="UnityEngine.Vector3Int" /> center offset from (X0, Z0).</param>
        /// <param name="size"><see cref="UnityEngine.Vector3Int" /> size of noise map using X and Z values.</param>
        /// <param name="worldGenerationSettings"><see cref="WorldGenerationSettings" /> to use.</param>
        public void Generate(Vector3Int offset, Vector3Int size, WorldGenerationSettings worldGenerationSettings)
        {
            Ready = false;

            ChangeCenter(offset, size);
            PerlinNoise.GenerateMap(ref Map, offset, worldGenerationSettings);

            Ready = true;
        }

        private void ChangeCenter(Vector3Int center, Vector3Int size)
        {
            Bounds = new BoundsInt(center - new Vector3Int(size.x / 2, 0, size.z / 2), size);
        }

        /// <summary>
        ///     Get a <see cref="T:float[][]" /> section of the current noise map.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3Int" /> center of the section.</param>
        /// <param name="size"><see cref="UnityEngine.Vector3Int" /> size of the section, using X and Z.</param>
        /// <returns>
        ///     <see cref="T:float[][]" /> section of the current noise map, centered around given
        ///     <see cref="UnityEngine.Vector3Int" /> position.
        /// </returns>
        public float[] GetSection(Vector3Int position, Vector3Int size)
        {
            if (!Mathv.ContainsVector3Int(Bounds, position))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to retrieve noise map by offset: offset ({position.x}, {position.z}) outside of noise map.");
                return null;
            }

            Vector3Int indexes = position - Bounds.min;

            float[] noiseMap = new float[size.x * size.z];

            for (int index = 0; index < noiseMap.Length; index++)
            {
                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, size);

                noiseMap[index] = Map[indexes.x + x][indexes.z + z];
            }

            return noiseMap;
        }
    }
}
