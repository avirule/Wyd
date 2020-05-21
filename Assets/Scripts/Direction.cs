#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Wyd.World.Chunks.Generation;

#endregion

namespace Wyd
{
    /// <summary>
    ///     6-way cardinal directions in byte values.
    /// </summary>
    [Flags]
    public enum Direction : byte
    {
        /// <summary>
        ///     Positive on X axis
        /// </summary>
        East =
            0b0000_0001,

        /// <summary>
        ///     Positive on Y axis
        /// </summary>
        Up =
            0b0000_0010,

        /// <summary>
        ///     Positive on Z axis
        /// </summary>
        North =
            0b0000_0100,

        /// <summary>
        ///     Negative on X axis
        /// </summary>
        West =
            0b0000_1000,

        /// <summary>
        ///     Negative on Y axis
        /// </summary>
        Down =
            0b0001_0000,

        /// <summary>
        ///     Negative on Z axis
        /// </summary>
        South =
            0b0010_0000,


        Mask =
            0b0011_1111
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
            East = new int3(1, 0, 0);
            Up = new int3(0, 1, 0);
            North = new int3(0, 0, 1);
            West = new int3(-1, 0, 0);
            Down = new int3(0, -1, 0);
            South = new int3(0, 0, -1);

            CardinalDirectionNormals = new[]
            {
                North,
                East,
                South,
                West
            };

            AllDirectionNormals = new[]
            {
                East,
                Up,
                North,
                West,
                Down,
                South,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Direction NormalToDirection(float3 normal)
        {
            if (normal.x > 0)
            {
                return Direction.East;
            }
            else if (normal.x < 0)
            {
                return Direction.West;
            }

            if (normal.y > 0)
            {
                return Direction.Up;
            }
            else if (normal.y < 0)
            {
                return Direction.Down;
            }

            if (normal.z > 0)
            {
                return Direction.North;
            }
            else if (normal.z < 0)
            {
                return Direction.South;
            }

            return Direction.Mask;
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

            _DirectionAsOrderPlacement = new ReadOnlyDictionary<Direction, int>(directionAsOrderPlacement);


            _DirectionsAsAxes = new Dictionary<Direction, int3>
            {
                { Direction.North, Directions.North },
                { Direction.East, Directions.East },
                { Direction.South, Directions.South },
                { Direction.West, Directions.West },
                { Direction.Up, Directions.Up },
                { Direction.Down, Directions.Down },
                { Direction.Mask, int3.zero }
            };

            _DirectionsAsIndexStep = new Dictionary<Direction, int>
            {
                { Direction.North, GenerationConstants.CHUNK_SIZE },
                { Direction.East, 1 },
                { Direction.South, -GenerationConstants.CHUNK_SIZE },
                { Direction.West, -1 },
                { Direction.Up, GenerationConstants.CHUNK_SIZE_SQUARED },
                { Direction.Down, -GenerationConstants.CHUNK_SIZE_SQUARED }
            };
        }

        private static readonly IReadOnlyDictionary<Direction, int> _DirectionAsOrderPlacement;
        private static readonly IReadOnlyDictionary<Direction, int3> _DirectionsAsAxes;
        private static readonly IReadOnlyDictionary<Direction, int> _DirectionsAsIndexStep;

        public static int OrderPlacement(this Direction direction) => _DirectionAsOrderPlacement[direction];

        public static int3 AsInt3(this Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return Directions.North;
                case Direction.East:
                    return Directions.East;
                case Direction.South:
                    return Directions.South;
                case Direction.West:
                    return Directions.West;
                case Direction.Up:
                    return Directions.Up;
                case Direction.Down:
                    return Directions.Down;
                case Direction.Mask:
                    return int3.zero;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static int AsIndexStep(this Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return GenerationConstants.CHUNK_SIZE;
                case Direction.East:
                    return 1;
                case Direction.South:
                    return -GenerationConstants.CHUNK_SIZE;
                case Direction.West:
                    return -1;
                case Direction.Up:
                    return GenerationConstants.CHUNK_SIZE_SQUARED;
                case Direction.Down:
                    return -GenerationConstants.CHUNK_SIZE_SQUARED;
                case Direction.Mask:
                    return default;
                default:
                    return default;
            }
        }

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
