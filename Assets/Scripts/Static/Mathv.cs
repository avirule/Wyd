using UnityEngine;

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
            return new Vector3(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z));
        }

        public static Vector3 Multiply(this Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        public static Vector3 Divide(this Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        public static Vector3 Mod(this Vector3 a, Vector3 mod)
        {
            return new Vector3(a.x % mod.x, a.y % mod.y, a.z % mod.z);
        }

        public static Vector3 Floor(this Vector3 a)
        {
            return new Vector3(Mathf.Floor(a.x), Mathf.Floor(a.y), Mathf.Floor(a.z));
        }

        #endregion


        #region Vector3Int

        public static Vector3Int Abs(this Vector3Int a)
        {
            return new Vector3Int(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z));
        }

        public static Vector3Int Multiply(this Vector3Int a, Vector3Int b)
        {
            return new Vector3Int(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        #endregion
    }
}