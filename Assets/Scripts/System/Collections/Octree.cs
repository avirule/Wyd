#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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


        #region GetPoint

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetPoint(float3 point) => GetPointIterative(point);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetPointRecursive(float3 point) => _RootNode.GetPoint(_Size / 2f, point.x, point.y, point.z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetPointIterative(float3 point)
        {
            OctreeNode currentNode = _RootNode;
            float x = point.x, y = point.y, z = point.z;

            for (float extent = _Size / 2f; !currentNode.IsUniform; extent /= 2f)
            {
                DetermineOctant(extent, ref x, ref y, ref z, out int octant);

                currentNode = currentNode[octant];
            }

            return currentNode.Value;
        }

        #endregion


        #region SetPoint

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPoint(float3 point, ushort value) => SetPointRecursive(point, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetPointRecursive(float3 point, ushort value) => _RootNode.SetPoint(_Size / 2f, point.x, point.y, point.z, value);

        #endregion


        public IEnumerable<ushort> GetAllData()
        {
            for (int index = 0; index < math.pow(_Size, 3); index++)
            {
                yield return GetPoint(WydMath.IndexTo3D(index, _Size));
            }
        }

        #region Helper Methods

        // indexes:
        // bottom half quadrant indexes:
        // 1 3
        // 0 2
        // top half quadrant indexes:
        // 5 7
        // 4 6
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DetermineOctant(float extent, ref float x, ref float y, ref float z, out int octant)
        {
            octant = 0;

            if (x >= extent)
            {
                x -= extent;
                octant += 1;
            }

            if (y >= extent)
            {
                y -= extent;
                octant += 4;
            }

            if (z >= extent)
            {
                z -= extent;
                octant += 2;
            }
        }

        #endregion
    }
}
