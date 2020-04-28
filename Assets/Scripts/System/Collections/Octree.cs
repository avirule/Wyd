#region

using System;
using System.Collections.Generic;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class Octree<T> : INodeCollection<T> where T : unmanaged, IEquatable<T>
    {
        private readonly byte _Size;
        private readonly OctreeNode<T> _RootNode;

        public T Value => _RootNode.Value;
        public bool IsUniform => _RootNode.IsUniform;

        public Octree(byte size, T initialValue)
        {
            if ((size <= 0) || ((size & (size - 1)) != 0))
            {
                throw new ArgumentException("Size must be a power of two.", nameof(size));
            }

            _Size = size;
            _RootNode = new OctreeNode<T>(initialValue);
        }

        public T GetPoint(float3 point) => _RootNode.GetPoint(_Size / 2f, point.x, point.y, point.z);

        public void SetPoint(float3 point, T value) => _RootNode.SetPoint(_Size / 2f, point.x, point.y, point.z, value);

        public IEnumerable<T> GetAllData()
        {
            for (int index = 0; index < math.pow(_Size, 3); index++)
            {
                yield return GetPoint(WydMath.IndexTo3D(index, _Size));
            }
        }
    }
}
