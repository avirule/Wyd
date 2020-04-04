#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Unity.Mathematics;
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
        public static int3 North { get; }
        public static int3 East { get; }
        public static int3 South { get; }
        public static int3 West { get; }
        public static int3 Up { get; }
        public static int3 Down { get; }

        public static readonly int3[] CardinalDirectionAxes =
        {
            North,
            East,
            South,
            West
        };

        public static readonly int3[] AllDirectionAxes =
        {
            North,
            East,
            South,
            West,
            Up,
            Down
        };

        static Directions()
        {
            North = new int3(0, 0, 1);
            East = new int3(1, 0, 0);
            South = new int3(0, 0, -1);
            West = new int3(-1, 0, 0);
            Up = new int3(0, 1, 0);
            Down = new int3(0, -1, 0);
        }
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


            DirectionsAsAxes = new Dictionary<Direction, int3>
            {
                { Direction.North, Directions.North },
                { Direction.East, Directions.East },
                { Direction.South, Directions.South },
                { Direction.West, Directions.West },
                { Direction.Up, Directions.Up },
                { Direction.Down, Directions.Down },
                { Direction.All, int3.zero }
            };

            DirectionsAsIndexStep = new Dictionary<Direction, int>
            {
                { Direction.North, ChunkController.Size.x },
                { Direction.East, 1 },
                { Direction.South, -ChunkController.Size.x },
                { Direction.West, -1 },
                { Direction.Up, ChunkController.Size.x * ChunkController.Size.z },
                { Direction.Down, -(ChunkController.Size.x * ChunkController.Size.z) }
            };
        }

        private static readonly IReadOnlyDictionary<Direction, int> DirectionAsOrderPlacement;
        private static readonly IReadOnlyDictionary<Direction, int3> DirectionsAsAxes;
        private static readonly IReadOnlyDictionary<Direction, int> DirectionsAsIndexStep;

        public static int OrderPlacement(this Direction direction) => DirectionAsOrderPlacement[direction];

        public static int3 AsInt3(this Direction direction) => DirectionsAsAxes[direction];

        public static int AsIndexStep(this Direction direction) => DirectionsAsIndexStep[direction];

        public static bool IsPositiveNormal(this Direction direction) =>
            (direction == Direction.North) || (direction == Direction.East) || (direction == Direction.Up);

        public static float FromVector3Axis(this Direction direction, int3 a)
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
