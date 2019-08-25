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

        /// <summary>
        ///     Calculates absolute value of given <see cref="UnityEngine.Vector3"/>.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3"/>.</param>
        /// <returns>Absolute value of given <see cref="UnityEngine.Vector3"/>.</returns>
        /// <seealso cref="Abs(Vector3Int)"/>
        public static Vector3 Abs(this Vector3 a)
        {
            a.x = Mathf.Abs(a.x);
            a.y = Mathf.Abs(a.y);
            a.z = Mathf.Abs(a.z);

            return a;
        }

        /// <summary>
        ///     Calculates product of two <see cref="UnityEngine.Vector3"/>.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3"/>.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3"/> to multiply with.</param>
        /// <returns>Product of the two <see cref="UnityEngine.Vector3"/>.</returns>
        /// <seealso cref="Multiply(Vector3Int, Vector3Int)"/>
        public static Vector3 Multiply(this Vector3 a, Vector3 b)
        {
            a.x *= b.x;
            a.y *= b.y;
            a.z *= b.z;

            return a;
        }

        /// <summary>
        ///     Calculates quotient of two <see cref="UnityEngine.Vector3"/>.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3"/>.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3"/> to divide with.</param>
        /// <returns>Quotient of the two <see cref="UnityEngine.Vector3"/>.</returns>
        /// <seealso cref="Divide(Vector3Int, Vector3Int)"/>
        public static Vector3 Divide(this Vector3 a, Vector3 b)
        {
            a.x /= b.x;
            a.y /= b.y;
            a.z /= b.z;

            return a;
        }

        /// <summary>
        ///     Calculates <see cref="UnityEngine.Vector3"/> of division remainders.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3"/>.</param>
        /// <param name="mod"><see cref="UnityEngine.Vector3"/> to use as mod.</param>
        /// <returns><see cref="UnityEngine.Vector3"/> of division remainders.</returns>
        /// <seealso cref="Mod(Vector3Int, Vector3Int)"/>
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

        /// <summary>
        ///     Calculates absolute value of given <see cref="UnityEngine.Vector3Int"/>.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int"/>.</param>
        /// <returns>Absolute value of given <see cref="UnityEngine.Vector3Int"/>.</returns>
        /// <seealso cref="Abs(Vector3)"/>
        public static Vector3Int Abs(this Vector3Int a)
        {
            a.x = Mathf.Abs(a.x);
            a.y = Mathf.Abs(a.y);
            a.z = Mathf.Abs(a.z);

            return a;
        }

        /// <summary>
        ///     Calculates product of two <see cref="UnityEngine.Vector3Int"/>.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int"/>.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3Int"/> to multiply with.</param>
        /// <returns>Product of the two <see cref="UnityEngine.Vector3Int"/>.</returns>
        /// <seealso cref="Multiply(Vector3, Vector3)"/>
        public static Vector3Int Multiply(this Vector3Int a, Vector3Int b)
        {
            a.x *= b.x;
            a.y *= b.y;
            a.z *= b.z;

            return a;
        }

        /// <summary>
        ///     Calculates quotient of two <see cref="UnityEngine.Vector3Int"/>.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int"/>.</param>
        /// <param name="b"><see cref="UnityEngine.Vector3Int"/> to divide with.</param>
        /// <returns>Quotient of the two <see cref="UnityEngine.Vector3Int"/>.</returns>
        /// <seealso cref="Divide(Vector3, Vector3)"/>
        public static Vector3Int Divide(this Vector3Int a, Vector3Int b)
        {
            a.x /= b.x;
            a.y /= b.y;
            a.z /= b.z;

            return a;
        }
        
        /// <summary>
        ///     Calculates <see cref="UnityEngine.Vector3Int"/> of division remainders.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int"/>.</param>
        /// <param name="mod"><see cref="UnityEngine.Vector3Int"/> to use as mod.</param>
        /// <returns><see cref="UnityEngine.Vector3Int"/> of division remainders.</returns>
        /// <seealso cref="Mod(Vector3, Vector3)"/>
        public static Vector3Int Mod(this Vector3Int a, Vector3Int mod)
        {
            a.x %= mod.x;
            a.y %= mod.y;
            a.z %= mod.z;

            return a;
        }

        /// <summary>
        ///     Calculates total product of all <see cref="System.Int32"/> of a <see cref="UnityEngine.Vector3Int"/>.
        /// </summary>
        /// <param name="a">Given <see cref="UnityEngine.Vector3Int"/>.</param>
        /// <returns><see cref="System.Int32"/> product of all <see cref="System.Int32"/> of a <see cref="UnityEngine.Vector3Int"/>.</returns>
        public static int Product(this Vector3Int a)
        {
            return a.x * a.y * a.z;
        }

        /// <summary>
        ///     Calculates 3D <see cref="System.Int32"/> index from 1D <see cref="System.Int32"/> index, given a <see cref="UnityEngine.Vector3Int"/> size in 3D space.
        /// </summary>
        /// <param name="index">1D <see cref="System.Int32"/> index.</param>
        /// <param name="size3d"><see cref="UnityEngine.Vector3Int"/> size in 3D space.</param>
        /// <returns><see cref="T:Tuple{int, int, int}"/> (x, y, z) of 3D coordinates.</returns>
        public static (int, int, int) GetVector3IntIndex(int index, Vector3Int size3d)
        {
            int xQuotient = Math.DivRem(index, size3d.x, out int x);
            int zQuotient = Math.DivRem(xQuotient, size3d.z, out int z);
            int y = zQuotient % size3d.y;

            return (x, y, z);
        }

        /// <summary>
        ///     Calculates 1D <see cref="System.Int32"/> index from 3D <see cref="UnityEngine.Vector3Int"/>, given a <see cref="UnityEngine.Vector3Int"/> size in 3D space.
        /// </summary>
        /// <param name="a">3D <see cref="UnityEngine.Vector3Int"/> index.</param>
        /// <param name="size3d"><see cref="UnityEngine.Vector3Int"/> size in 3D space.</param>
        /// <returns>1D <see cref="System.Int32"/> index.</returns>
        public static int To1D(this Vector3Int a, Vector3Int size3d)
        {
            return a.x + (a.z * size3d.x) + (a.y * size3d.x * size3d.z);
        }

        #endregion
    }
}