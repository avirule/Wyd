#region

using System;
using System.Collections.Generic;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode : INodeCollection<ushort>
    {
        private readonly int _Size;

        private OctreeNode[] _Nodes;

        public ushort Value { get; private set; }

        public bool IsUniform => _Nodes == null;

        public OctreeNode(int size, ushort value)
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
                new OctreeNode(extent, Value),
                new OctreeNode(extent, Value),
                new OctreeNode(extent, Value),
                new OctreeNode(extent, Value),
                new OctreeNode(extent, Value),
                new OctreeNode(extent, Value),
                new OctreeNode(extent, Value),
                new OctreeNode(extent, Value)
            };
        }


        #region Data Operations

        public ushort GetPoint(float3 point)
        {
            if (IsUniform)
            {
                return Value;
            }

            int extent = _Size / 2;

            (int x, int y, int z, int octant) = DetermineOctant(point, extent);

            return _Nodes[octant].GetPoint(point - (new float3(x, y, z) * extent));
        }

        public void SetPoint(float3 point, ushort newValue)
        {
            if (IsUniform && (Value == newValue))
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

            (int x, int y, int z, int octant) = DetermineOctant(point, extent);

            // recursively dig into octree and set
            _Nodes[octant].SetPoint(point - (new float3(x, y, z) * extent), newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (CheckShouldCollapse())
            {
                Collapse();
            }
        }

        private bool CheckShouldCollapse()
        {
            if (IsUniform)
            {
                return false;
            }

            ushort firstValue = _Nodes[0].Value;

            // avoiding using linq here for performance sensitivity
            for (int index = 0; index < 8 /* octants! */; index++)
            {
                OctreeNode node = _Nodes[index];

                if (!node.IsUniform || (node.Value.GetHashCode() != firstValue.GetHashCode()))
                {
                    return false;
                }
            }

            return true;
        }

        public IEnumerable<ushort> GetAllData()
        {
            for (int index = 0; index < math.pow(_Size, 3); index++)
            {
                yield return GetPoint(WydMath.IndexTo3D(index, _Size));
            }
        }

        #endregion


        #region Helper Methods

        // private static readonly int3 One = new int3(1, 1, 1);
        // private static readonly int3 OctantComponents = new int3(1, 4, 2);

        // indexes:
        // bottom half quadrant:
        // 1 3
        // 0 2
        // top half quadrant:
        // 5 7
        // 4 6
        private static (int, int, int, int) DetermineOctant(float3 point, int extent)
        {
            // bool3 componentOffset = point >= extent;
            //
            // int3 octantAxes = math.select(int3.zero, One, componentOffset);
            // int octant = math.csum(math.select(int3.zero, OctantComponents, componentOffset));
            //
            // return (octant, octantAxes);

            int x = 0, y = 0, z = 0, octant = 0;

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

            return (x, y, z, octant);
        }

        #endregion
    }
}
