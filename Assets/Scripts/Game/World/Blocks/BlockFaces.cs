#region

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

#endregion

namespace Wyd.Game.World.Blocks
{
    public struct BlockFaces
    {
        public static class Vertices
        {
            public static readonly IReadOnlyDictionary<Direction, float3[]> FaceVerticesByDirection;
            public static readonly float3[][] FaceVerticesByNormalIndex;

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

            static Vertices()
            {
                FaceVerticesByDirection = new Dictionary<Direction, float3[]>
                {
                    { Direction.North, North },
                    { Direction.East, East },
                    { Direction.South, South },
                    { Direction.West, West },
                    { Direction.Up, Up },
                    { Direction.Down, Down }
                };

                FaceVerticesByNormalIndex = new[]
                {
                    East,
                    Up,
                    North,
                    West,
                    Down,
                    South
                };
            }
        }

        public static class Triangles
        {
            public static readonly IReadOnlyDictionary<Direction, int[]> FaceTrianglesByDirection;
            public static readonly int[][] FaceTrianglesByNormalIndex;

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
                FaceTrianglesByDirection = new Dictionary<Direction, int[]>
                {
                    { Direction.North, North },
                    { Direction.East, East },
                    { Direction.South, South },
                    { Direction.West, West },
                    { Direction.Up, Up },
                    { Direction.Down, Down }
                };

                FaceTrianglesByNormalIndex = new[]
                {
                    East,
                    Up,
                    North,
                    West,
                    Down,
                    South
                };
            }
        }

        private Direction _RawValue;

        public BlockFaces(Direction direction = 0) => _RawValue = direction;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAnyFaces() => _RawValue > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAllFaces() => (_RawValue & Direction.Mask) == Direction.Mask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFace(Direction direction) => (_RawValue & direction) == direction;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFace(Direction direction)
        {
            _RawValue |= direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsetFace(Direction direction)
        {
            _RawValue &= ~direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearFaces()
        {
            _RawValue = 0;
        }
    }
}
