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

        public static int3[] CardinalDirectionNormals { get; }

        public static int3[] AllDirectionNormals { get; }

        static Directions()
        {
            North = new int3(0, 0, 1);
            East = new int3(1, 0, 0);
            South = new int3(0, 0, -1);
            West = new int3(-1, 0, 0);
            Up = new int3(0, 1, 0);
            Down = new int3(0, -1, 0);

            CardinalDirectionNormals = new[]
            {
                North,
                East,
                South,
                West
            };

            AllDirectionNormals = new[]
            {
                North,
                East,
                South,
                West,
                Up,
                Down
            };
        }

        public static IEnumerable<Direction> NormalToDirections(float3 normal)
        {
            if (normal.x > 0)
            {
                yield return Direction.East;
            }
            else if (normal.x < 0)
            {
                yield return Direction.West;
            }

            if (normal.y > 0)
            {
                yield return Direction.Up;
            }
            else if (normal.y < 0)
            {
                yield return Direction.Down;
            }

            if (normal.z > 0)
            {
                yield return Direction.North;
            }
            else if (normal.z < 0)
            {
                yield return Direction.South;
            }
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

            _directionAsOrderPlacement = new ReadOnlyDictionary<Direction, int>(directionAsOrderPlacement);


            _directionsAsAxes = new Dictionary<Direction, int3>
            {
                { Direction.North, Directions.North },
                { Direction.East, Directions.East },
                { Direction.South, Directions.South },
                { Direction.West, Directions.West },
                { Direction.Up, Directions.Up },
                { Direction.Down, Directions.Down },
                { Direction.All, int3.zero }
            };

            _directionsAsIndexStep = new Dictionary<Direction, int>
            {
                { Direction.North, ChunkController.Size3D.x },
                { Direction.East, 1 },
                { Direction.South, -ChunkController.Size3D.x },
                { Direction.West, -1 },
                { Direction.Up, ChunkController.Size3D.x * ChunkController.Size3D.z },
                { Direction.Down, -(ChunkController.Size3D.x * ChunkController.Size3D.z) }
            };
        }

        private static readonly IReadOnlyDictionary<Direction, int> _directionAsOrderPlacement;
        private static readonly IReadOnlyDictionary<Direction, int3> _directionsAsAxes;
        private static readonly IReadOnlyDictionary<Direction, int> _directionsAsIndexStep;

        public static int OrderPlacement(this Direction direction) => _directionAsOrderPlacement[direction];

        public static int3 ToInt3(this Direction direction) => _directionsAsAxes[direction];

        public static int AsIndexStep(this Direction direction) => _directionsAsIndexStep[direction];

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
