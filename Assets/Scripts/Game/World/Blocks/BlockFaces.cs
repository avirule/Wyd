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
                new float3(1f, 0f, 0f),
                new float3(1f, 0f, 1f),
                new float3(1f, 1f, 0f),
                new float3(1f, 1f, 1f)
            };

            public static readonly float3[] South =
            {
                new float3(0f, 0f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(1f, 1f, 0f)
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
                new float3(0f, 1f, 0f),
                new float3(1f, 1f, 0f),
                new float3(0f, 1f, 1f),
                new float3(1f, 1f, 1f)
            };

            public static readonly float3[] Down =
            {
                new float3(0f, 0f, 0f),
                new float3(0f, 0f, 1f),
                new float3(1f, 0f, 0f),
                new float3(1f, 0f, 1f)
            };

            static Vertices() =>
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

        private const byte FACES_MASK = 0b0011_1111;

        public byte RawValue;

        public BlockFaces(byte rawValue = 0) => RawValue = rawValue;

        public bool HasAnyFaces() => RawValue > 0;

        public bool HasAllFaces() => (RawValue & FACES_MASK) >= FACES_MASK;

        public bool HasFace(Direction direction) => (RawValue & (byte)direction) > 0;

        public void SetFace(Direction direction, bool boolean)
        {
            RawValue = RawValue.SetBitByBoolWithMask((byte)direction, boolean);
        }

        public void ClearFaces()
        {
            RawValue = 0;
        }
    }
}
