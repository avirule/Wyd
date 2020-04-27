#region

using System.Runtime.CompilerServices;
using Unity.Mathematics;

#endregion

namespace Wyd.System
{
    public static class OpenSimplexSlim
    {
        private const short _FN_INLINE = 256;
        private const int _X_PRIME = 1619;
        private const int _Y_PRIME = 31337;
        private const int _Z_PRIME = 6971;

        public static float GetSimplex(int seed, float frequency, int2 coords) =>
            Simplex2D(seed, frequency, coords.x, coords.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSimplex(int seed, float frequency, int3 coords) =>
            Simplex3D(seed, frequency, coords.x, coords.y, coords.z);


        #region Simplex2D

        private static readonly float2[] _grad2D =
        {
            new float2(-1, -1),
            new float2(1, -1),
            new float2(-1, 1),
            new float2(1, 1),
            new float2(0, -1),
            new float2(-1, 0),
            new float2(0, 1),
            new float2(1, 0)
        };

        private const float _F2 = (float)(1.0 / 2.0);
        private const float _G2 = (float)(1.0 / 4.0);

        [MethodImpl(_FN_INLINE)]
        private static float GradCoord2D(int seed, int x, int y, float xd, float yd)
        {
            int hash = seed;
            hash ^= _X_PRIME * x;
            hash ^= _Y_PRIME * y;

            hash = hash * hash * hash * 60493;
            hash = (hash >> 13) ^ hash;

            float2 g = _grad2D[hash & 7];

            return (xd * g.x) + (yd * g.y);
        }

        private static float Simplex2D(int seed, float frequency, float x, float y)
        {
            x *= frequency;
            y *= frequency;

            float t = (x + y) * _F2;
            int i = FastFloor(x + t);
            int j = FastFloor(y + t);

            t = (i + j) * _G2;
            float X0 = i - t;
            float Y0 = j - t;

            float x0 = x - X0;
            float y0 = y - Y0;

            int i1, j1;
            if (x0 > y0)
            {
                i1 = 1;
                j1 = 0;
            }
            else
            {
                i1 = 0;
                j1 = 1;
            }

            float x1 = (x0 - i1) + _G2;
            float y1 = (y0 - j1) + _G2;
            float x2 = (x0 - 1) + _F2;
            float y2 = (y0 - 1) + _F2;

            float n0, n1, n2;

            t = (float)0.5 - (x0 * x0) - (y0 * y0);
            if (t < 0)
            {
                n0 = 0;
            }
            else
            {
                t *= t;
                n0 = t * t * GradCoord2D(seed, i, j, x0, y0);
            }

            t = (float)0.5 - (x1 * x1) - (y1 * y1);
            if (t < 0)
            {
                n1 = 0;
            }
            else
            {
                t *= t;
                n1 = t * t * GradCoord2D(seed, i + i1, j + j1, x1, y1);
            }

            t = (float)0.5 - (x2 * x2) - (y2 * y2);
            if (t < 0)
            {
                n2 = 0;
            }
            else
            {
                t *= t;
                n2 = t * t * GradCoord2D(seed, i + 1, j + 1, x2, y2);
            }

            return 50 * (n0 + n1 + n2);
        }

        #endregion


        #region Simplex3D

        private const float _F3 = (float)(1.0 / 3.0);
        private const float _G3 = (float)(1.0 / 6.0);
        private const float _G33 = (_G3 * 3) - 1;

        private static readonly float3[] _grad3D =
        {
            new float3(1, 1, 0),
            new float3(-1, 1, 0),
            new float3(1, -1, 0),
            new float3(-1, -1, 0),
            new float3(1, 0, 1),
            new float3(-1, 0, 1),
            new float3(1, 0, -1),
            new float3(-1, 0, -1),
            new float3(0, 1, 1),
            new float3(0, -1, 1),
            new float3(0, 1, -1),
            new float3(0, -1, -1),
            new float3(1, 1, 0),
            new float3(0, -1, 1),
            new float3(-1, 1, 0),
            new float3(0, -1, -1)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastFloor(float f) => f >= 0 ? (int)f : (int)f - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GradCoord3D(int seed, int x, int y, int z, float xd, float yd, float zd)
        {
            int hash = seed;
            hash ^= _X_PRIME * x;
            hash ^= _Y_PRIME * y;
            hash ^= _Z_PRIME * z;

            hash = hash * hash * hash * 60493;
            hash = (hash >> 13) ^ hash;

            float3 g = _grad3D[hash & 15];

            return (xd * g.x) + (yd * g.y) + (zd * g.z);
        }

        private static float Simplex3D(int seed, float frequency, float x, float y, float z)
        {
            x *= frequency;
            y *= frequency;
            z *= frequency;

            float t = (x + y + z) * _F3;
            int i = FastFloor(x + t);
            int j = FastFloor(y + t);
            int k = FastFloor(z + t);

            t = (i + j + k) * _G3;
            float x0 = x - (i - t);
            float y0 = y - (j - t);
            float z0 = z - (k - t);

            int i1, j1, k1;
            int i2, j2, k2;

            if (x0 >= y0)
            {
                if (y0 >= z0)
                {
                    i1 = 1;
                    j1 = 0;
                    k1 = 0;
                    i2 = 1;
                    j2 = 1;
                    k2 = 0;
                }
                else if (x0 >= z0)
                {
                    i1 = 1;
                    j1 = 0;
                    k1 = 0;
                    i2 = 1;
                    j2 = 0;
                    k2 = 1;
                }
                else // x0 < z0
                {
                    i1 = 0;
                    j1 = 0;
                    k1 = 1;
                    i2 = 1;
                    j2 = 0;
                    k2 = 1;
                }
            }
            else // x0 < y0
            {
                if (y0 < z0)
                {
                    i1 = 0;
                    j1 = 0;
                    k1 = 1;
                    i2 = 0;
                    j2 = 1;
                    k2 = 1;
                }
                else if (x0 < z0)
                {
                    i1 = 0;
                    j1 = 1;
                    k1 = 0;
                    i2 = 0;
                    j2 = 1;
                    k2 = 1;
                }
                else // x0 >= z0
                {
                    i1 = 0;
                    j1 = 1;
                    k1 = 0;
                    i2 = 1;
                    j2 = 1;
                    k2 = 0;
                }
            }

            float x1 = (x0 - i1) + _G3;
            float y1 = (y0 - j1) + _G3;
            float z1 = (z0 - k1) + _G3;
            float x2 = (x0 - i2) + _F3;
            float y2 = (y0 - j2) + _F3;
            float z2 = (z0 - k2) + _F3;
            float x3 = x0 + _G33;
            float y3 = y0 + _G33;
            float z3 = z0 + _G33;

            float n0, n1, n2, n3;

            t = (float)0.6 - (x0 * x0) - (y0 * y0) - (z0 * z0);
            if (t < 0)
            {
                n0 = 0;
            }
            else
            {
                t *= t;
                n0 = t * t * GradCoord3D(seed, i, j, k, x0, y0, z0);
            }

            t = (float)0.6 - (x1 * x1) - (y1 * y1) - (z1 * z1);
            if (t < 0)
            {
                n1 = 0;
            }
            else
            {
                t *= t;
                n1 = t * t * GradCoord3D(seed, i + i1, j + j1, k + k1, x1, y1, z1);
            }

            t = (float)0.6 - (x2 * x2) - (y2 * y2) - (z2 * z2);
            if (t < 0)
            {
                n2 = 0;
            }
            else
            {
                t *= t;
                n2 = t * t * GradCoord3D(seed, i + i2, j + j2, k + k2, x2, y2, z2);
            }

            t = (float)0.6 - (x3 * x3) - (y3 * y3) - (z3 * z3);
            if (t < 0)
            {
                n3 = 0;
            }
            else
            {
                t *= t;
                n3 = t * t * GradCoord3D(seed, i + 1, j + 1, k + 1, x3, y3, z3);
            }

            return 32 * (n0 + n1 + n2 + n3);
        }

        #endregion
    }
}
