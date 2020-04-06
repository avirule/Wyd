#region

using System;
using Unity.Mathematics;

#endregion

namespace Wyd.System
{
    public static class WydMath
    {
        public static int3 IndexTo3D(int index, int3 bounds)
        {
            int xQuotient = Math.DivRem(index, bounds.x, out int x);
            int zQuotient = Math.DivRem(xQuotient, bounds.z, out int z);
            int y = zQuotient % bounds.y;
            return new int3(x, y, z);
        }

        #region ToFloat()

        public static float3 ToFloat(int3 a) => new float3(a.x, a.y, a.z);

        #endregion

        #region ToInt()

        public static int3 ToInt(float3 a) => new int3((int)a.x, (int)a.y, (int)a.z);

        #endregion

        #region RoundBy()

        public static float3 RoundBy(float3 a, float3 b) => math.floor(a / b) * b;

        #endregion

        #region Product()

        public static float Product(float3 a) => a.x * a.y * a.z;

        public static int Product(int3 a) => a.x * a.y * a.z;

        #endregion
    }
}
