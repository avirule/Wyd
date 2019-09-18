#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Game.World.Blocks
{
    public static class BlockFaces
    {
        public static class Vertices
        {
            public static readonly IReadOnlyDictionary<Direction, Vector3[]> FaceVertices;

            public static readonly Vector3[] North =
            {
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 1f, 1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(1f, 1f, 1f)
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

            static Vertices()
            {
                FaceVertices = new Dictionary<Direction, Vector3[]>
                {
                    { Direction.North, North },
                    { Direction.East, East },
                    { Direction.South, South },
                    { Direction.West, West },
                    { Direction.Up, Up },
                    { Direction.Down, Down }
                };
            }
        }

        public static class Triangles
        {
            public static readonly IReadOnlyDictionary<Direction, int[]> FaceTriangles;

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

            static Triangles()
            {
                FaceTriangles = new Dictionary<Direction, int[]>
                {
                    { Direction.North, North },
                    { Direction.East, East },
                    { Direction.South, South },
                    { Direction.West, West },
                    { Direction.Up, Up },
                    { Direction.Down, Down }
                };
            }
        }
    }
}
