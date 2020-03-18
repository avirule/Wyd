#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;

#endregion

namespace Wyd.Game
{
    /// <summary>
    ///     6-way cardinal directions in byte values.
    /// </summary>
    public enum Direction : byte
    {
        /// <summary>
        ///     Positive on Z axis
        /// </summary>
        North = 0b0000_0001,

        /// <summary>
        ///     Positive on X axis
        /// </summary>
        East = 0b0000_0010,

        /// <summary>
        ///     Negative on Z axis
        /// </summary>
        South = 0b0000_0100,

        /// <summary>
        ///     Negative on X axis
        /// </summary>
        West = 0b0000_1000,

        /// <summary>
        ///     Positive on Y axis
        /// </summary>
        Up = 0b0001_0000,

        /// <summary>
        ///     Negative on Y axis
        /// </summary>
        Down = 0b0010_0000,
        All = 0b0011_1111
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
            Dictionary<Direction, int> directionAsOrderPlacement = new Dictionary<Direction, int>();

            int index = 0;
            foreach (Direction direction in (Direction[])Enum.GetValues(typeof(Direction)))
            {
                directionAsOrderPlacement.Add(direction, index);
                index += 1;
            }

            DirectionAsOrderPlacement = new ReadOnlyDictionary<Direction, int>(directionAsOrderPlacement);


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

        private static readonly IReadOnlyDictionary<Direction, int> DirectionAsOrderPlacement;
        private static readonly IReadOnlyDictionary<Direction, Vector3> DirectionsAsVector3;
        private static readonly IReadOnlyDictionary<Direction, int> DirectionsAsIndexStep;

        public static int OrderPlacement(this Direction direction) => DirectionAsOrderPlacement[direction];

        public static Vector3 AsVector3(this Direction direction) => DirectionsAsVector3[direction];

        public static int AsIndexStep(this Direction direction) => DirectionsAsIndexStep[direction];

        public static bool IsPositiveNormal(this Direction direction) =>
            (direction == Direction.North) || (direction == Direction.East) || (direction == Direction.Up);

        public static float FromVector3Axis(this Direction direction, Vector3Int a)
        {
            switch (direction)
            {
                case Direction.North:
                case Direction.South:
                    return a.z;
                case Direction.East:
                case Direction.West:
                    return a.x;
                case Direction.Up:
                case Direction.Down:
                    return a.y;
            }

            return 0f;
        }
    }
}
