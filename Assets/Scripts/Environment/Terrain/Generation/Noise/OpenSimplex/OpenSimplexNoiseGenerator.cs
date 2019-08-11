#region

using Threading;
using UnityEngine;

#endregion

namespace Environment.Terrain.Generation.Noise.OpenSimplex
{
    public class OpenSimplexNoiseGenerator : ThreadedProcess
    {
        private static OpenSimplexNoise openSimplex;
        private readonly AnimationCurve _Curve;
        private readonly Vector3Int _Offset;
        private readonly Vector3Int _Size;

        public float[][] Map;

        public OpenSimplexNoiseGenerator(long seed, Vector3Int offset, Vector3Int size, AnimationCurve curve = null)
        {
            if (openSimplex == null)
            {
                openSimplex = new OpenSimplexNoise(seed);
            }

            _Offset = offset;
            _Size = size;
            _Curve = curve;
        }

        protected override void ThreadFunction()
        {
            base.ThreadFunction();

            float maxNoiseHeight = float.MinValue;
            float minNoiseHeight = float.MaxValue;

            Map = new float[_Size.x][];

            for (int x = 0; x < _Size.x; x++)
            {
                Map[x] = new float[_Size.z];

                for (int z = 0; z < _Size.z; z++)
                {
                    float noiseHeight = (float) openSimplex.Evaluate(_Offset.x + x, _Offset.z + z);

                    if (noiseHeight > maxNoiseHeight)
                    {
                        maxNoiseHeight = noiseHeight;
                    }
                    else if (noiseHeight < minNoiseHeight)
                    {
                        minNoiseHeight = noiseHeight;
                    }

                    Map[x][z] = noiseHeight;
                }
            }

            for (int x = 0; x < _Size.x; x++)
            {
                for (int z = 0; z < _Size.z; z++)
                {
                    float inverseLerp = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, Map[x][z]);

                    Map[x][z] = _Curve?.Evaluate(inverseLerp) ?? inverseLerp;
                }
            }
        }
    }
}