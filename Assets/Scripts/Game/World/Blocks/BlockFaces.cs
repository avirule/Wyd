#region

using System;
using UnityEngine;

#endregion

namespace Game.World.Blocks
{
    public static class BlockFaces
    {
        public static class Vertices
        {
            public static readonly Vector3[] North =
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 1f, 1f),
                new Vector3(2f, 0f, 1f),
                new Vector3(2f, 1f, 1f)
            };

            public static readonly Vector3[] East =
            {
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 1f),
                new Vector3(1f, 1f, 0f),
                new Vector3(1f, 1f, 1f)
            };

            public static readonly Vector3[] South =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f)
            };

            public static readonly Vector3[] West =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 1f, 1f)
            };

            public static readonly Vector3[] Up =
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 1f),
                new Vector3(1f, 1f, 1f)
            };

            public static readonly Vector3[] Down =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 1f)
            };

            public static Vector3[] Get(Direction direction)
            {
                switch (direction)
                {
                    case Direction.North:
                        return North;
                    case Direction.East:
                        return East;
                    case Direction.South:
                        return South;
                    case Direction.West:
                        return West;
                    case Direction.Up:
                        return Up;
                    case Direction.Down:
                        return Down;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }
            }
        }

        public static class Triangles
        {
            public static readonly int[] North =
            {
                0,
                2,
                1,
                2,
                3,
                1
            };

            public static readonly int[] East =
            {
                0,
                2,
                1,
                2,
                3,
                1
            };

            public static readonly int[] South =
            {
                0,
                2,
                1,
                2,
                3,
                1
            };

            public static readonly int[] West =
            {
                0,
                2,
                1,
                2,
                3,
                1
            };

            public static readonly int[] Up =
            {
                0,
                2,
                1,
                2,
                3,
                1
            };

            public static readonly int[] Down =
            {
                0,
                2,
                1,
                2,
                3,
                1
            };

            public static int[] Get(Direction direction)
            {
                switch (direction)
                {
                    case Direction.North:
                        return North;
                    case Direction.East:
                        return East;
                    case Direction.South:
                        return South;
                    case Direction.West:
                        return West;
                    case Direction.Up:
                        return Up;
                    case Direction.Down:
                        return Down;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }
            }
        }
    }
}