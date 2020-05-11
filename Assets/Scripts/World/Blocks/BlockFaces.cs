#region

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

#endregion

namespace Wyd.World.Blocks
{
    public struct BlockFaces
    {
        public static class Vertices
        {
            public static readonly IReadOnlyDictionary<Direction, int3[]> FaceVerticesByDirection;


            public static readonly int3[] North =
            {
                new int3(0, 0, 1),
                new int3(0, 1, 1),
                new int3(1, 0, 1),
                new int3(1, 1, 1)
            };

            public static readonly int3[] East =
            {
                new int3(1, 0, 1),
                new int3(1, 1, 1),
                new int3(1, 0, 0),
                new int3(1, 1, 0)
            };

            public static readonly int3[] South =
            {
                new int3(1, 0, 0),
                new int3(1, 1, 0),
                new int3(0, 0, 0),
                new int3(0, 1, 0)
            };

            public static readonly int3[] West =
            {
                new int3(0, 0, 0),
                new int3(0, 1, 0),
                new int3(0, 0, 1),
                new int3(0, 1, 1)
            };

            public static readonly int3[] Up =
            {
                new int3(0, 1, 1),
                new int3(0, 1, 0),
                new int3(1, 1, 1),
                new int3(1, 1, 0)
            };

            public static readonly int3[] Down =
            {
                new int3(0, 0, 0),
                new int3(0, 0, 1),
                new int3(1, 0, 0),
                new int3(1, 0, 1)
            };

            static Vertices() =>
                FaceVerticesByDirection = new Dictionary<Direction, int3[]>
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
            public static readonly int[] FaceTriangles =
            {
                0,
                2,
                1,
                2,
                3,
                1
            };
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
