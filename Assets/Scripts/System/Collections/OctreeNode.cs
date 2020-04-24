#region

using System;
using System.Collections.Generic;
using Serilog;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode<T> where T : unmanaged
    {
        private readonly int _Size;

        private OctreeNode<T>[] _Nodes;

        public T Value { get; private set; }

        public bool IsUniform => _Nodes == null;

        public OctreeNode(int size, T value)
        {
            // check if size is power of two
            if ((size <= 0) || ((size & (size - 1)) != 0))
            {
                throw new ArgumentException("Size must be a power of two.", nameof(size));
            }

            _Nodes = null;
            _Size = size;
            Value = value;
        }

        private void Collapse()
        {
            if (IsUniform)
            {
                return;
            }

            Value = _Nodes[0].Value;
            _Nodes = null;
        }

        private void Populate(int extent)
        {
            _Nodes = new[]
            {
                new OctreeNode<T>(extent, Value),
                new OctreeNode<T>(extent, Value),
                new OctreeNode<T>(extent, Value),
                new OctreeNode<T>(extent, Value),
                new OctreeNode<T>(extent, Value),
                new OctreeNode<T>(extent, Value),
                new OctreeNode<T>(extent, Value),
                new OctreeNode<T>(extent, Value)
            };
        }


        #region Data Operations

        public T GetPoint(float3 point)
        {
            if (IsUniform)
            {
                return Value;
            }

            int extent = _Size / 2;
            int4 result = DetermineOctant(point, extent);

            return _Nodes[result.w].GetPoint(point - (result.xyz * extent));
        }

        public void SetPoint(float3 point, T newValue)
        {
            if (IsUniform && (Value.GetHashCode() == newValue.GetHashCode()))
            {
                // operation does nothing, so return
                return;
            }

            if (_Size == 1)
            {
                // reached smallest possible depth (usually 1x1x1) so
                // set value and return
                Value = newValue;
                return;
            }

            int extent = _Size / 2;

            if (IsUniform)
            {
                // node has no child nodes, so populate
                Populate(extent);
            }

            int4 result = DetermineOctant(point, extent);

            // recursively dig into octree and set
            _Nodes[result.w].SetPoint(point - (result.xyz * extent), newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (CheckShouldCollapse())
            {
                Collapse();
            }
        }

        private bool CheckShouldCollapse()
        {
            if (!IsUniform)
            {
                return false;
            }

            T firstValue = _Nodes[0].Value;

            // avoiding using linq here for performance sensitivity
            for (int index = 0; index < 8 /* octants! */; index++)
            {
                OctreeNode<T> node = _Nodes[index];

                if (!node.IsUniform || (node.Value.GetHashCode() != firstValue.GetHashCode()))
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
        private static int4 DetermineOctant(float3 point, int extent)
        {
            int4 result = int4.zero;

            if (point.x >= extent)
            {
                result += new int4(1, 0, 0, 1);
            }

            if (point.y >= extent)
            {
                result += new int4(0, 1, 0, 4);
            }

            if (point.z >= extent)
            {
                result += new int4(0, 0, 1, 2);
            }

            return result;
        }

        #endregion
    }
}
