#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode<T> : INodeCollection<T> where T : unmanaged
    {
        #region Instance Members

        private readonly byte _Size;

        private OctreeNode<T>[] _Nodes;
        private bool _IsUniform;
        private T _Value;

        public T Value => _Value;
        public bool IsUniform => _IsUniform;

        #endregion

        public OctreeNode(byte size, T value)
        {
            // check if size is power of two
            if ((size <= 0) || ((size & (size - 1)) != 0))
            {
                throw new ArgumentException("Size must be a power of two.", nameof(size));
            }

            _Size = size;
            _Value = value;
            _IsUniform = true;
            _Nodes = null;
        }


        #region Data Operations

        public T GetPoint(float3 point)
        {
            if (_IsUniform)
            {
                return _Value;
            }

            int extent = _Size / 2;

            DetermineOctant(point, extent, out int x, out int y, out int z, out int octant);

            return _Nodes[octant].GetPoint(point - (new float3(x, y, z) * extent));
        }

        public void SetPoint(float3 point, T newValue)
        {
            int extent = _Size / 2;

            if (_IsUniform)
            {
                if (_Value.GetHashCode() == newValue.GetHashCode())
                {
                    return;
                }
                else if (_Size == 1)
                {
                    // reached smallest possible depth (usually 1x1x1) so
                    // set value and return
                    _Value = newValue;
                    return;
                }
                else
                {
                    _IsUniform = false;
                    _Nodes = new[]
                    {
                        new OctreeNode<T>((byte)extent, _Value),
                        new OctreeNode<T>((byte)extent, _Value),
                        new OctreeNode<T>((byte)extent, _Value),
                        new OctreeNode<T>((byte)extent, _Value),
                        new OctreeNode<T>((byte)extent, _Value),
                        new OctreeNode<T>((byte)extent, _Value),
                        new OctreeNode<T>((byte)extent, _Value),
                        new OctreeNode<T>((byte)extent, _Value)
                    };
                }
            }

            DetermineOctant(point, extent, out int x, out int y, out int z, out int octant);

            // recursively dig into octree and set
            _Nodes[octant].SetPoint(point - (new float3(x, y, z) * extent), newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (CheckShouldCollapse())
            {
                _IsUniform = true;
                _Value = _Nodes[0]._Value;
                _Nodes = null;
            }
        }

        private bool CheckShouldCollapse()
        {
            if (_IsUniform)
            {
                return false;
            }

            T firstValue = _Nodes[0]._Value;

            // avoiding using linq here for performance sensitivity
            for (int index = 0; index < 8 /* octants! */; index++)
            {
                OctreeNode<T> node = _Nodes[index];

                if (!node._IsUniform || (node._Value.GetHashCode() != firstValue.GetHashCode()))
                {
                    return false;
                }
            }

            return true;
        }

        public IEnumerable<T> GetAllData()
        {
            for (int index = 0; index < math.pow(_Size, 3); index++)
            {
                yield return GetPoint(WydMath.IndexTo3D(index, _Size));
            }
        }

        #endregion


        #region Helper Methods

        // indexes:
        // bottom half quadrant:
        // 1 3
        // 0 2
        // top half quadrant:
        // 5 7
        // 4 6
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DetermineOctant(float3 point, int extent, out int x, out int y, out int z, out int octant)
        {
            x = y = z = octant = 0;

            if (point.x >= extent)
            {
                x = 1;
                octant += 1;
            }

            if (point.y >= extent)
            {
                y = 1;
                octant += 4;
            }

            if (point.z >= extent)
            {
                z = 1;
                octant += 2;
            }
        }

        #endregion
    }
}
