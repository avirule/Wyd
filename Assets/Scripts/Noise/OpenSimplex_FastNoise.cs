//#define FN_USE_DOUBLES

// NOISE FILE TAKEN FROM: https://github.com/Auburns/FastNoise_CSharp

#region

#if FN_USE_DOUBLES
#else
using FN_DECIMAL = System.Single;
#endif
using System.Runtime.CompilerServices;

#endregion

namespace Noise
{
    public class OpenSimplex_FastNoise
    {
        private const short FN_INLINE = 256; //(Int16)MethodImplOptions.AggressiveInlining;

        private readonly int m_seed = 1337;
        private const float m_frequency = (float) 0.01;

        public OpenSimplex_FastNoise(int seed = 1337) => m_seed = seed;

        private struct Float2
        {
            public readonly float x, y;

            public Float2(float x, float y)
            {
                this.x = x;
                this.y = y;
            }
        }

        private struct Float3
        {
            public readonly float x, y, z;

            public Float3(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
        }

        private static readonly Float2[] GRAD_2D =
        {
            new Float2(-1, -1),
            new Float2(1, -1),
            new Float2(-1, 1),
            new Float2(1, 1),
            new Float2(0, -1),
            new Float2(-1, 0),
            new Float2(0, 1),
            new Float2(1, 0)
        };

        private static readonly Float3[] GRAD_3D =
        {
            new Float3(1, 1, 0),
            new Float3(-1, 1, 0),
            new Float3(1, -1, 0),
            new Float3(-1, -1, 0),
            new Float3(1, 0, 1),
            new Float3(-1, 0, 1),
            new Float3(1, 0, -1),
            new Float3(-1, 0, -1),
            new Float3(0, 1, 1),
            new Float3(0, -1, 1),
            new Float3(0, 1, -1),
            new Float3(0, -1, -1),
            new Float3(1, 1, 0),
            new Float3(0, -1, 1),
            new Float3(-1, 1, 0),
            new Float3(0, -1, -1)
        };

        [MethodImpl(FN_INLINE)]
        private static int FastFloor(float f) => f >= 0 ? (int) f : (int) f - 1;

        // Hashing
        private const int X_PRIME = 1619;
        private const int Y_PRIME = 31337;
        private const int Z_PRIME = 6971;
        private const int W_PRIME = 1013;

        [MethodImpl(FN_INLINE)]
        private static float GradCoord2D(int seed, int x, int y, float xd, float yd)
        {
            int hash = seed;
            hash ^= X_PRIME * x;
            hash ^= Y_PRIME * y;

            hash = hash * hash * hash * 60493;
            hash = (hash >> 13) ^ hash;

            Float2 g = GRAD_2D[hash & 7];

            return (xd * g.x) + (yd * g.y);
        }

        [MethodImpl(FN_INLINE)]
        private static float GradCoord3D(int seed, int x, int y, int z, float xd, float yd, float zd)
        {
            int hash = seed;
            hash ^= X_PRIME * x;
            hash ^= Y_PRIME * y;
            hash ^= Z_PRIME * z;

            hash = hash * hash * hash * 60493;
            hash = (hash >> 13) ^ hash;

            Float3 g = GRAD_3D[hash & 15];

            return (xd * g.x) + (yd * g.y) + (zd * g.z);
        }

        [MethodImpl(FN_INLINE)]
        private static float GradCoord4D(int seed, int x, int y, int z, int w, float xd, float yd, float zd, float wd)
        {
            int hash = seed;
            hash ^= X_PRIME * x;
            hash ^= Y_PRIME * y;
            hash ^= Z_PRIME * z;
            hash ^= W_PRIME * w;

            hash = hash * hash * hash * 60493;
            hash = (hash >> 13) ^ hash;

            hash &= 31;
            float a = yd, b = zd, c = wd; // X,Y,Z
            switch (hash >> 3)
            {
                // OR, DEPENDING ON HIGH ORDER 2 BITS:
                case 1:
                    a = wd;
                    b = xd;
                    c = yd;
                    break; // W,X,Y
                case 2:
                    a = zd;
                    b = wd;
                    c = xd;
                    break; // Z,W,X
                case 3:
                    a = yd;
                    b = zd;
                    c = wd;
                    break; // Y,Z,W
            }

            return ((hash & 4) == 0 ? -a : a) + ((hash & 2) == 0 ? -b : b) + ((hash & 1) == 0 ? -c : c);
        }

        public static float GetSimplex(int seed, float freq, float x, float y, float z) =>
            SingleSimplex(seed, x * freq, y * freq, z * freq);

        private const float F3 = (float) (1.0 / 3.0);
        private const float G3 = (float) (1.0 / 6.0);
        private const float G33 = (G3 * 3) - 1;

        private static float SingleSimplex(int seed, float x, float y, float z)
        {
            float t = (x + y + z) * F3;
            int i = FastFloor(x + t);
            int j = FastFloor(y + t);
            int k = FastFloor(z + t);

            t = (i + j + k) * G3;
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

            float x1 = (x0 - i1) + G3;
            float y1 = (y0 - j1) + G3;
            float z1 = (z0 - k1) + G3;
            float x2 = (x0 - i2) + F3;
            float y2 = (y0 - j2) + F3;
            float z2 = (z0 - k2) + F3;
            float x3 = x0 + G33;
            float y3 = y0 + G33;
            float z3 = z0 + G33;

            float n0, n1, n2, n3;

            t = (float) 0.6 - (x0 * x0) - (y0 * y0) - (z0 * z0);
            if (t < 0)
            {
                n0 = 0;
            }
            else
            {
                t *= t;
                n0 = t * t * GradCoord3D(seed, i, j, k, x0, y0, z0);
            }

            t = (float) 0.6 - (x1 * x1) - (y1 * y1) - (z1 * z1);
            if (t < 0)
            {
                n1 = 0;
            }
            else
            {
                t *= t;
                n1 = t * t * GradCoord3D(seed, i + i1, j + j1, k + k1, x1, y1, z1);
            }

            t = (float) 0.6 - (x2 * x2) - (y2 * y2) - (z2 * z2);
            if (t < 0)
            {
                n2 = 0;
            }
            else
            {
                t *= t;
                n2 = t * t * GradCoord3D(seed, i + i2, j + j2, k + k2, x2, y2, z2);
            }

            t = (float) 0.6 - (x3 * x3) - (y3 * y3) - (z3 * z3);
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

        public float GetSimplex(float x, float y) => SingleSimplex(m_seed, x * m_frequency, y * m_frequency);

        private const float F2 = (float) (1.0 / 2.0);
        private const float G2 = (float) (1.0 / 4.0);

        private static float SingleSimplex(int seed, float x, float y)
        {
            float t = (x + y) * F2;
            int i = FastFloor(x + t);
            int j = FastFloor(y + t);

            t = (i + j) * G2;
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

            float x1 = (x0 - i1) + G2;
            float y1 = (y0 - j1) + G2;
            float x2 = (x0 - 1) + F2;
            float y2 = (y0 - 1) + F2;

            float n0, n1, n2;

            t = (float) 0.5 - (x0 * x0) - (y0 * y0);
            if (t < 0)
            {
                n0 = 0;
            }
            else
            {
                t *= t;
                n0 = t * t * GradCoord2D(seed, i, j, x0, y0);
            }

            t = (float) 0.5 - (x1 * x1) - (y1 * y1);
            if (t < 0)
            {
                n1 = 0;
            }
            else
            {
                t *= t;
                n1 = t * t * GradCoord2D(seed, i + i1, j + j1, x1, y1);
            }

            t = (float) 0.5 - (x2 * x2) - (y2 * y2);
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

        public float GetSimplex(float x, float y, float z, float w) => SingleSimplex(m_seed, x * m_frequency,
            y * m_frequency, z * m_frequency, w * m_frequency);

        private static readonly byte[] SIMPLEX_4D =
        {
            0,
            1,
            2,
            3,
            0,
            1,
            3,
            2,
            0,
            0,
            0,
            0,
            0,
            2,
            3,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            2,
            3,
            0,
            0,
            2,
            1,
            3,
            0,
            0,
            0,
            0,
            0,
            3,
            1,
            2,
            0,
            3,
            2,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            3,
            2,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            2,
            0,
            3,
            0,
            0,
            0,
            0,
            1,
            3,
            0,
            2,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            3,
            0,
            1,
            2,
            3,
            1,
            0,
            1,
            0,
            2,
            3,
            1,
            0,
            3,
            2,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            0,
            3,
            1,
            0,
            0,
            0,
            0,
            2,
            1,
            3,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            0,
            1,
            3,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            3,
            0,
            1,
            2,
            3,
            0,
            2,
            1,
            0,
            0,
            0,
            0,
            3,
            1,
            2,
            0,
            2,
            1,
            0,
            3,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            3,
            1,
            0,
            2,
            0,
            0,
            0,
            0,
            3,
            2,
            0,
            1,
            3,
            2,
            1,
            0
        };

        private const float F4 = (float) ((2.23606797 - 1.0) / 4.0);
        private const float G4 = (float) ((5.0 - 2.23606797) / 20.0);

        private float SingleSimplex(int seed, float x, float y, float z, float w)
        {
            float n0, n1, n2, n3, n4;
            float t = (x + y + z + w) * F4;
            int i = FastFloor(x + t);
            int j = FastFloor(y + t);
            int k = FastFloor(z + t);
            int l = FastFloor(w + t);
            t = (i + j + k + l) * G4;
            float X0 = i - t;
            float Y0 = j - t;
            float Z0 = k - t;
            float W0 = l - t;
            float x0 = x - X0;
            float y0 = y - Y0;
            float z0 = z - Z0;
            float w0 = w - W0;

            int c = x0 > y0 ? 32 : 0;
            c += x0 > z0 ? 16 : 0;
            c += y0 > z0 ? 8 : 0;
            c += x0 > w0 ? 4 : 0;
            c += y0 > w0 ? 2 : 0;
            c += z0 > w0 ? 1 : 0;
            c <<= 2;

            int i1 = SIMPLEX_4D[c] >= 3 ? 1 : 0;
            int i2 = SIMPLEX_4D[c] >= 2 ? 1 : 0;
            int i3 = SIMPLEX_4D[c++] >= 1 ? 1 : 0;
            int j1 = SIMPLEX_4D[c] >= 3 ? 1 : 0;
            int j2 = SIMPLEX_4D[c] >= 2 ? 1 : 0;
            int j3 = SIMPLEX_4D[c++] >= 1 ? 1 : 0;
            int k1 = SIMPLEX_4D[c] >= 3 ? 1 : 0;
            int k2 = SIMPLEX_4D[c] >= 2 ? 1 : 0;
            int k3 = SIMPLEX_4D[c++] >= 1 ? 1 : 0;
            int l1 = SIMPLEX_4D[c] >= 3 ? 1 : 0;
            int l2 = SIMPLEX_4D[c] >= 2 ? 1 : 0;
            int l3 = SIMPLEX_4D[c] >= 1 ? 1 : 0;

            float x1 = (x0 - i1) + G4;
            float y1 = (y0 - j1) + G4;
            float z1 = (z0 - k1) + G4;
            float w1 = (w0 - l1) + G4;
            float x2 = (x0 - i2) + (2 * G4);
            float y2 = (y0 - j2) + (2 * G4);
            float z2 = (z0 - k2) + (2 * G4);
            float w2 = (w0 - l2) + (2 * G4);
            float x3 = (x0 - i3) + (3 * G4);
            float y3 = (y0 - j3) + (3 * G4);
            float z3 = (z0 - k3) + (3 * G4);
            float w3 = (w0 - l3) + (3 * G4);
            float x4 = (x0 - 1) + (4 * G4);
            float y4 = (y0 - 1) + (4 * G4);
            float z4 = (z0 - 1) + (4 * G4);
            float w4 = (w0 - 1) + (4 * G4);

            t = (float) 0.6 - (x0 * x0) - (y0 * y0) - (z0 * z0) - (w0 * w0);
            if (t < 0)
            {
                n0 = 0;
            }
            else
            {
                t *= t;
                n0 = t * t * GradCoord4D(seed, i, j, k, l, x0, y0, z0, w0);
            }

            t = (float) 0.6 - (x1 * x1) - (y1 * y1) - (z1 * z1) - (w1 * w1);
            if (t < 0)
            {
                n1 = 0;
            }
            else
            {
                t *= t;
                n1 = t * t * GradCoord4D(seed, i + i1, j + j1, k + k1, l + l1, x1, y1, z1, w1);
            }

            t = (float) 0.6 - (x2 * x2) - (y2 * y2) - (z2 * z2) - (w2 * w2);
            if (t < 0)
            {
                n2 = 0;
            }
            else
            {
                t *= t;
                n2 = t * t * GradCoord4D(seed, i + i2, j + j2, k + k2, l + l2, x2, y2, z2, w2);
            }

            t = (float) 0.6 - (x3 * x3) - (y3 * y3) - (z3 * z3) - (w3 * w3);
            if (t < 0)
            {
                n3 = 0;
            }
            else
            {
                t *= t;
                n3 = t * t * GradCoord4D(seed, i + i3, j + j3, k + k3, l + l3, x3, y3, z3, w3);
            }

            t = (float) 0.6 - (x4 * x4) - (y4 * y4) - (z4 * z4) - (w4 * w4);
            if (t < 0)
            {
                n4 = 0;
            }
            else
            {
                t *= t;
                n4 = t * t * GradCoord4D(seed, i + 1, j + 1, k + 1, l + 1, x4, y4, z4, w4);
            }

            return 27 * (n0 + n1 + n2 + n3 + n4);
        }
    }
}
