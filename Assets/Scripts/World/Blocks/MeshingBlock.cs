using System.Runtime.CompilerServices;

namespace Wyd.World.Blocks
{
    public struct MeshingBlock
    {
        private Direction _Faces;

        public ushort ID { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAnyFaces() => _Faces > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasAllFaces() => (_Faces & Direction.Mask) == Direction.Mask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFace(Direction direction) => (_Faces & direction) == direction;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFace(Direction direction)
        {
            _Faces |= direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsetFace(Direction direction)
        {
            _Faces &= ~direction;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearFaces()
        {
            _Faces = 0;
        }
    }
}
