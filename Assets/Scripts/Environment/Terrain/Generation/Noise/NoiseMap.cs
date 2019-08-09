using System;
using Logging;
using NLog;
using Static;
using UnityEngine;

namespace Environment.Terrain.Generation.Noise
{
    public class NoiseMap
    {
        public BoundsInt Bounds;
        public float[][] Map;
        public bool Ready;

        public NoiseMap(float[][] map, Vector3Int center, Vector3Int size, bool createArray = false)
        {
            Map = map;
            Bounds = new BoundsInt(center - new Vector3Int(size.x / 2, 0, size.z / 2), size);

            Ready = false;

            if (!createArray)
            {
                return;
            }

            StaticMethods.CreateArray(ref Map, size);
        }

        public float[][] GetSection(Vector3Int position, Vector3Int size)
        {
            if (!Mathv.ContainsVector3Int(Bounds, position))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to retrieve noise map by offset: offset ({position.x}, {position.z}) outside of noise map.");
                return null;
            }

            Vector3Int indexes = position - Bounds.min;
            float[][] noiseMap = new float[size.x][];

            for (int x = 0; x < noiseMap.Length; x++)
            {
                noiseMap[x] = new float[size.z];

                for (int z = 0; z < noiseMap[0].Length; z++)
                {
                    try
                    {
                        noiseMap[x][z] = Map[indexes.x + x][indexes.z + z];
                    }
                    catch (IndexOutOfRangeException)
                    {
                    }
                }
            }

            return noiseMap;
        }
    }
}