#region

using System;
using UnityEngine;

#endregion

namespace Game
{
    /// <summary>
    ///     6-way cardinal directions in byte values.
    /// </summary>
    public enum Direction : byte
    {
        /// <summary>
        ///     Positive on Z axis
        /// </summary>
        North = 0b00000001,

        /// <summary>
        ///     Positive on X axis
        /// </summary>
        East = 0b00000010,

        /// <summary>
        ///     Negative on Z axis
        /// </summary>
        South = 0b00000100,

        /// <summary>
        ///     Negative on X axis
        /// </summary>
        West = 0b00001000,

        /// <summary>
        ///     Positive on Y axis
        /// </summary>
        Up = 0b00010000,

        /// <summary>
        ///     Negative on Y axis
        /// </summary>
        Down = 0b00100000
    }

    public static class DirectionExtensions
    {
        public static Vector3 AsVector3(this Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return Vector3.forward;
                case Direction.East:
                    return Vector3.right;
                case Direction.South:
                    return Vector3.back;
                case Direction.West:
                    return Vector3.left;
                case Direction.Up:
                    return Vector3.up;
                case Direction.Down:
                    return Vector3.down;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}