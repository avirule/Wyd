#region

using System;
using System.Collections.Generic;
using Controllers.World;
using UnityEngine;

#endregion

namespace Game
{
    /// <summary>
    ///     6-way cardinal directions in byte values.
    /// </summary>
    public enum Direction : sbyte
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
        Down = 0b00100000,
        All = 0b00111111
    }

    public static class Directions
    {
        public static readonly Vector3[] CardinalDirectionsVector3 =
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back,
            Vector3.left
        };
    }

    public static class DirectionExtensions
    {
        static DirectionExtensions()
        {
            DirectionsAsVector3 = new Dictionary<Direction, Vector3>
            {
                { Direction.North, Vector3.forward },
                { Direction.East, Vector3.right },
                { Direction.South, Vector3.back },
                { Direction.West, Vector3.left },
                { Direction.Up, Vector3.up },
                { Direction.Down, Vector3.down },
                { Direction.All, Vector3.zero }
            };

            DirectionsAsIndexStep = new Dictionary<Direction, int>
            {
                { Direction.North, ChunkController.Size.x },
                { Direction.East, 1 },
                { Direction.South, -ChunkController.Size.x },
                { Direction.West, -1 },
                { Direction.Up, ChunkController.YIndexStep },
                { Direction.Down, -ChunkController.YIndexStep }
            };
        }

        private static readonly IReadOnlyDictionary<Direction, Vector3> DirectionsAsVector3;
        private static readonly IReadOnlyDictionary<Direction, int> DirectionsAsIndexStep;

        public static Vector3 AsVector3(this Direction direction)
        {
            return DirectionsAsVector3[direction];
        }

        public static int AsIndexStep(this Direction direction)
        {
            return DirectionsAsIndexStep[direction];
        }

        public static bool IsPositive(this Direction direction)
        {
            return (direction == Direction.North) || (direction == Direction.East) || (direction == Direction.Up);
        }
    }
}
