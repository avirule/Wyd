#region

using System;
using UnityEngine;

#endregion

namespace Wyd.System
{
    public static class Mathv
    {
        #region Vector3

        public static Vector3 Half = new Vector3(0.5f, 0.5f, 0.5f);

        /// <summary>
        ///     Casts all <see cref="float" /> of given <see cref="UnityEngine.Vector3" /> to <see cref="int" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3" />.</param>
        /// <returns>
        ///     <see cref="UnityEngine.Vector3Int" /> of <see cref="int" /> casted values from given
        ///     <see cref="UnityEngine.Vector3" />.
        /// </returns>
        public static Vector3Int ToInt(this Vector3 a) => new Vector3Int((int) a.x, (int) a.y, (int) a.z);

        public static bool GreaterThanVector3(Vector3 a, Vector3 b) => (a.x > b.x) || (a.y > b.y) || (a.z > b.z);

        public static bool LessThanVector3(Vector3 a, Vector3 b) => (a.x < b.x) || (a.y < b.y) || (a.z < b.z);

        public static bool ContainsVector3(Bounds bounds, Vector3 point) =>
            (point.x >= bounds.min.x)
            && (point.z >= bounds.min.z)
            && (point.x <= bounds.max.x)
            && (point.z <= bounds.max.z);

        public static bool ContainsVector3Int(BoundsInt bounds, Vector3Int point) =>
            (point.x >= bounds.min.x)
            && (point.z >= bounds.min.z)
            && (point.x <= bounds.max.x)
            && (point.z <= bounds.max.z);

        /// <summary>
        ///     Calculates absolute value of given <see cref="UnityEngine.Vector3" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3" />.</param>
        /// <returns>Absolute value of given <see cref="UnityEngine.Vector3" />.</returns>
        /// <seealso cref="Abs(Vector3Int)" />
        public static Vector3 Abs(this Vector3 a) => new Vector3(Math.Abs(a.x), Math.Abs(a.y), Math.Abs(a.z));

        /// <summary>
        ///     Calculates product of two <see cref="UnityEngine.Vector3" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3" />.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3" /> to multiply with.</param>
        /// <returns>Product of the two <see cref="UnityEngine.Vector3" />.</returns>
        /// <seealso cref="Multiply(Vector3Int, Vector3Int)" />
        public static Vector3 Multiply(this Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        /// <summary>
        ///     Calculates quotient of two <see cref="UnityEngine.Vector3" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3" />.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3" /> to divide with.</param>
        /// <returns>Quotient of the two <see cref="UnityEngine.Vector3" />.</returns>
        /// <seealso cref="Divide(Vector3Int, Vector3Int)" />
        public static Vector3 Divide(this Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

        /// <summary>
        ///     Calculates <see cref="UnityEngine.Vector3" /> of division remainders.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3" />.</param>
        /// <param name="mod"><see cref="UnityEngine.Vector3" /> to use as mod.</param>
        /// <returns><see cref="UnityEngine.Vector3" /> of division remainders.</returns>
        /// <seealso cref="Mod(Vector3Int, Vector3Int)" />
        public static Vector3 Mod(this Vector3 a, Vector3 mod) => new Vector3(a.x % mod.x, a.y % mod.y, a.z % mod.z);

        public static float Product(this Vector3 a) => a.x * a.y * a.z;

        public static Vector3 Round(this Vector3 a) =>
            new Vector3(Mathf.Round(a.x), Mathf.Round(a.y), Mathf.Round(a.z));

        public static Vector3 Floor(this Vector3 a) =>
            new Vector3(Mathf.Floor(a.x), Mathf.Floor(a.y), Mathf.Floor(a.z));

        public static Vector3 Ceil(this Vector3 a) => new Vector3(Mathf.Ceil(a.x), Mathf.Ceil(a.y), Mathf.Ceil(a.z));

        public static Vector3 Trunc(this Vector3 a) => new Vector3((float) Math.Truncate(a.x),
            (float) Math.Truncate(a.x),
            (float) Math.Truncate(a.x));

        public static bool AllLessThan(this Vector3 a, Vector3 b) => (a.x < b.x) && (a.y < b.y) && (a.z < b.z);

        public static bool AllLessThanOrEqual(this Vector3 a, Vector3 b) =>
            (a.x <= b.x) && (a.y <= b.y) && (a.z <= b.z);

        public static bool AllGreaterThan(this Vector3 a, Vector3 b) => (a.x > b.x) && (a.y > b.y) && (a.z > b.z);

        public static bool AllGreaterThanOrEqual(this Vector3 a, Vector3 b) =>
            (a.x >= b.x) && (a.y >= b.y) && (a.z >= b.z);

        public static bool AnyGreaterThanOrEqual(this Vector3 a, Vector3 b) =>
            (a.x >= b.x) || (a.y >= b.y) || (a.z >= b.z);

        public static float Sum(this Vector3 a) => a.x + a.y + a.z;

        public static float Max(this Vector3 a) => Mathf.Max(a.x, a.y, a.z);

        public static Vector3Int FloorToInt(this Vector3 a)
        {
            return new Vector3Int(Mathf.FloorToInt(a.x), Mathf.FloorToInt(a.y), Mathf.FloorToInt(a.z));
        }
        
        public static Vector3 RoundBy(this Vector3 a, Vector3 roundBy) => a.Divide(roundBy).Floor().Multiply(roundBy);

        /// <summary>
        ///     Calculates 1D <see cref="int" /> index from 3D <see cref="UnityEngine.Vector3Int" />, given a
        ///     <see cref="UnityEngine.Vector3Int" /> size in 3D space.
        /// </summary>
        /// <param name="a">3D <see cref="UnityEngine.Vector3Int" /> index.</param>
        /// <param name="size3d"><see cref="UnityEngine.Vector3Int" /> size in 3D space.</param>
        /// <param name="floor"></param>
        /// <returns>1D <see cref="int" /> index.</returns>
        public static int To1D(this Vector3 a, Vector3 size3d, bool floor = false)
        {
            if (floor)
            {
                a.Floor();
            }

            return (int) (a.x + (a.z * size3d.x) + (a.y * size3d.x * size3d.z));
        }

        public static float[] ToArray(this Vector3 a)
        {
            return new[]
            {
                a.x,
                a.y,
                a.z
            };
        }

        #endregion


        #region Vector3Int

        /// <summary>
        ///     Calculates absolute value of given <see cref="UnityEngine.Vector3Int" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int" />.</param>
        /// <returns>Absolute value of given <see cref="UnityEngine.Vector3Int" />.</returns>
        /// <seealso cref="Abs(Vector3)" />
        public static Vector3Int Abs(this Vector3Int a) => new Vector3Int(Math.Abs(a.x), Math.Abs(a.y), Math.Abs(a.z));

        /// <summary>
        ///     Calculates product of two <see cref="UnityEngine.Vector3Int" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int" />.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3Int" /> to multiply with.</param>
        /// <returns>Product of the two <see cref="UnityEngine.Vector3Int" />.</returns>
        /// <seealso cref="Multiply(Vector3, Vector3)" />
        public static Vector3Int Multiply(this Vector3Int a, Vector3Int b) =>
            new Vector3Int(a.x * b.x, a.y * b.y, a.z * b.z);

        /// <summary>
        ///     Calculates quotient of two <see cref="UnityEngine.Vector3Int" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int" />.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3Int" /> to divide with.</param>
        /// <returns>Quotient of the two <see cref="UnityEngine.Vector3Int" />.</returns>
        /// <seealso cref="Divide(Vector3, Vector3)" />
        public static Vector3Int Divide(this Vector3Int a, Vector3Int b) =>
            new Vector3Int(a.x / b.x, a.y / b.y, a.z / b.z);

        public static Vector3Int Divide(this Vector3Int a, int b) => new Vector3Int(a.x / b, a.y / b, a.z / b);

        /// <summary>
        ///     Calculates <see cref="UnityEngine.Vector3Int" /> of division remainders.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int" />.</param>
        /// <param name="mod"><see cref="UnityEngine.Vector3Int" /> to use as mod.</param>
        /// <returns><see cref="UnityEngine.Vector3Int" /> of division remainders.</returns>
        /// <seealso cref="Mod(Vector3, Vector3)" />
        public static Vector3Int Mod(this Vector3Int a, Vector3Int mod) =>
            new Vector3Int(a.x % mod.x, a.y % mod.y, a.z % mod.z);

        /// <summary>
        ///     Calculates total product of all <see cref="int" /> of a <see cref="UnityEngine.Vector3Int" />.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int" />.</param>
        /// <returns>
        ///     <see cref="int" /> product of all <see cref="int" /> of a
        ///     <see cref="UnityEngine.Vector3Int" />.
        /// </returns>
        public static int Product(this Vector3Int a) => a.x * a.y * a.z;

        /// <summary>
        ///     Calculates 3D <see cref="System.Int32" /> index from 1D <see cref="System.Int32" /> index, given a
        ///     <see cref="UnityEngine.Vector3Int" /> size in 3D space.
        /// </summary>
        /// <param name="index">1D <see cref="System.Int32" /> index.</param>
        /// <param name="size3d"><see cref="UnityEngine.Vector3Int" /> size in 3D space.</param>
        /// <returns><see cref="T:Tuple{int, int, int}" /> (x, y, z) of 3D coordinates.</returns>
        public static (int, int, int) GetIndexAs3D(int index, Vector3Int size3d)
        {
            int xQuotient = Math.DivRem(index, size3d.x, out int x);
            int zQuotient = Math.DivRem(xQuotient, size3d.z, out int z);
            int y = zQuotient % size3d.y;
            return (x, y, z);
        }

        public static Vector3Int GetIndexAsVector3Int(int index, Vector3Int size3d)
        {
            // todo optimize this a little?
            Vector3Int a = Vector3Int.zero;
            (a.x, a.y, a.z) = GetIndexAs3D(index, size3d);
            return a;
        }

        /// <summary>
        ///     Calculates 1D <see cref="int" /> index from 3D <see cref="UnityEngine.Vector3Int" />, given a
        ///     <see cref="UnityEngine.Vector3Int" /> size in 3D space.
        /// </summary>
        /// <param name="a">3D <see cref="UnityEngine.Vector3Int" /> index.</param>
        /// <param name="size3d"><see cref="UnityEngine.Vector3Int" /> size in 3D space.</param>
        /// <returns>1D <see cref="int" /> index.</returns>
        public static int To1D(this Vector3Int a, Vector3Int size3d) =>
            a.x + (a.z * size3d.x) + (a.y * size3d.x * size3d.z);

        public static int[] ToArray(this Vector3Int a)
        {
            return new[]
            {
                a.x,
                a.y,
                a.z
            };
        }

        #endregion
    }
}
