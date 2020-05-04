#region

using System;
using System.Collections.Generic;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class Octree : INodeCollection<ushort>
    {
        private readonly byte _Size;
        private readonly OctreeNode _RootNode;

        public ushort Value => _RootNode.Value;
        public bool IsUniform => _RootNode.IsUniform;

        public Octree(byte size, ushort initialValue)
        {
            if ((size <= 0) || ((size & (size - 1)) != 0))
            {
                throw new ArgumentException("Size must be a power of two.", nameof(size));
            }

            _Size = size;
            _RootNode = new OctreeNode(initialValue);
        }

        public ushort GetPoint(float3 point) => _RootNode.GetPoint(_Size / 2f, point.x, point.y, point.z);
        public void SetPoint(float3 point, ushort value) => _RootNode.SetPoint(_Size / 2f, point.x, point.y, point.z, value);

        public IEnumerable<ushort> GetAllData()
        {
            float extent = _Size / 2f;
            for (int index = 0; index < math.pow(_Size, 3); index++)
            {
                int3 coords = WydMath.IndexTo3D(index, _Size);

                yield return _RootNode.GetPoint(extent, coords.x, coords.y, coords.z);
            }
        }
    }
}
