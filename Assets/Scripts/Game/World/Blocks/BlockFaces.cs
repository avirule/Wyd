#region

using System.Collections.Generic;
using Unity.Mathematics;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Blocks
{
    public struct BlockFaces
    {
        public static class Vertices
        {
            public static readonly IReadOnlyDictionary<Direction, float3[]> FaceVertices;

            public static readonly float3[] North =
            {
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 1f),
                new float3(1f, 0f, 1f),
                new float3(1f, 1f, 1f)
            };

            public static readonly float3[] East =
            {
                new float3(1f, 0f, 1f),
                new float3(1f, 1f, 1f),
                new float3(1f, 0f, 0f),
                new float3(1f, 1f, 0f)
            };

            public static readonly float3[] South =
            {
                new float3(1f, 0f, 0f),
                new float3(1f, 1f, 0f),
                new float3(0f, 0f, 0f),
                new float3(0f, 1f, 0f)
            };

            public static readonly float3[] West =
            {
                new float3(0f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 1f),
                new float3(0f, 1f, 1f)
            };

            public static readonly float3[] Up =
            {
                new float3(0f, 1f, 1f),
                new float3(0f, 1f, 0f),
                new float3(1f, 1f, 1f),
                new float3(1f, 1f, 0f)
            };

            public static readonly float3[] Down =
            {
                new float3(0f, 0f, 0f),
                new float3(0f, 0f, 1f),
                new float3(1f, 0f, 0f),
                new float3(1f, 0f, 1f)
            };

            static Vertices() =>
                // todo make this a list and index into it
                FaceVertices = new Dictionary<Direction, float3[]>
                {
                    { Direction.North, North },
                    { Direction.East, East },
                    { Direction.South, South },
                    { Direction.West, West },
                    { Direction.Up, Up },
                    { Direction.Down, Down }
                };
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

            static Triangles() =>
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

        private Direction _RawValue;

        public BlockFaces(Direction direction = 0) => _RawValue = direction;

        public bool HasAnyFaces() => _RawValue > 0;

        public bool HasAllFaces() => (_RawValue & Direction.Mask) == Direction.Mask;

        public bool HasFace(Direction direction) => (_RawValue & direction) == direction;

        public void SetFace(Direction direction)
        {
            _RawValue |= direction;
        }

        public void UnsetFace(Direction direction)
        {
            _RawValue &= ~direction;
        }

        public void ClearFaces()
        {
            _RawValue = 0;
        }
    }
}
