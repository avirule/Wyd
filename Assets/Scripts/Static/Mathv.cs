#region

using System;
using UnityEngine;

#endregion

namespace Static
{
    public static class Mathv
    {
        #region Vector3

        public static bool GreaterThanVector3(Vector3 a, Vector3 b)
        {
            return (a.x > b.x) || (a.y > b.y) || (a.z > b.z);
        }

        public static bool LessThanVector3(Vector3 a, Vector3 b)
        {
            return (a.x < b.x) || (a.y < b.y) || (a.z < b.z);
        }

        public static bool ContainsVector3(Bounds bounds, Vector3 point)
        {
            return (point.x >= bounds.min.x) &&
                   (point.z >= bounds.min.z) &&
                   (point.x <= bounds.max.x) &&
                   (point.z <= bounds.max.z);
        }

        public static bool ContainsVector3Int(BoundsInt bounds, Vector3Int point)
        {
            return (point.x >= bounds.min.x) &&
                   (point.z >= bounds.min.z) &&
                   (point.x <= bounds.max.x) &&
                   (point.z <= bounds.max.z);
        }

        public static Vector3 Abs(this Vector3 a)
        {
            a.x = Mathf.Abs(a.x);
            a.y = Mathf.Abs(a.y);
            a.z = Mathf.Abs(a.z);

            return a;
        }

        public static Vector3 Multiply(this Vector3 a, Vector3 b)
        {
            a.x *= b.x;
            a.y *= b.y;
            a.z *= b.z;

            return a;
        }

        public static Vector3 Divide(this Vector3 a, Vector3 b)
        {
            a.x /= b.x;
            a.y /= b.y;
            a.z /= b.z;

            return a;
        }

        public static Vector3 Mod(this Vector3 a, Vector3 mod)
        {
            a.x %= mod.x;
            a.y %= mod.y;
            a.z %= mod.z;

            return a;
        }

        public static Vector3 Floor(this Vector3 a)
        {
            a.x = Mathf.Floor(a.x);
            a.y = Mathf.Floor(a.y);
            a.z = Mathf.Floor(a.z);

            return a;
        }

        #endregion


        #region Vector3Int

        public static Vector3Int Abs(this Vector3Int a)
        {
            a.x = Mathf.Abs(a.x);
            a.y = Mathf.Abs(a.y);
            a.z = Mathf.Abs(a.z);

            return a;
        }

        public static Vector3Int Multiply(this Vector3Int a, Vector3Int b)
        {
            a.x *= b.x;
            a.y *= b.y;
            a.z *= b.z;

            return a;
        }

        public static (int, int, int) GetVector3IntIndex(int index, Vector3Int maxSize)
        {
            int xQuotient = Math.DivRem(index, maxSize.x, out int x);
            int zQuotient = Math.DivRem(xQuotient, maxSize.z, out int z);
            int y = zQuotient % maxSize.y;

            return (x, y, z);
        }

        public static int To1D(this Vector3Int a, Vector3Int maxSize)
        {
            return a.x + (maxSize.x * (a.z + (maxSize.z * a.y)));
        }

        #endregion
    }
}