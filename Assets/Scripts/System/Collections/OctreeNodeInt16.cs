#region

using System.Collections.Generic;
using Unity.Mathematics;

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode : INodeCollection<ushort>
    {
        #region Instance Members

        private readonly byte _Size;

        private OctreeNode[] _Nodes;
        private bool _IsUniform;
        private ushort _Value;

        public ushort Value => _Value;
        public bool IsUniform => _IsUniform;

        #endregion

        /// <summary>
        ///     Creates an in-memory compressed 3D representation of any unmanaged data type.
        /// </summary>
        /// <param name="size">
        ///     Depth of the collection, or for instance the length of the cube. This need to be a power of 2.
        /// </param>
        /// <param name="value">Initial value of the collection.</param>
        public OctreeNode(byte size, ushort value)
        {
            _Size = size;
            _Value = value;
            _IsUniform = true;
            _Nodes = null;
        }


        #region Data Operations

        public ushort GetPoint(float3 point) => GetPoint(point.x, point.y, point.z);

        private ushort GetPoint(float x, float y, float z)
        {
            if (_IsUniform)
            {
                return _Value;
            }

            int extent = _Size / 2;

            DetermineOctant(x, y, z, extent, out float x0, out float y0, out float z0, out int octant);

            return _Nodes[octant].GetPoint(x - (x0 * extent), y - (y0 * extent), z - (z0 * extent));
        }

        public void SetPoint(float3 point, ushort newValue) => SetPoint(point.x, point.y, point.z, newValue);

        private void SetPoint(float x, float y, float z, ushort newValue)
        {
            int extent;

            if (_IsUniform)
            {
                if (_Value.GetHashCode() == newValue.GetHashCode())
                {
                    return;
                }
                else if (_Size == 0b1)
                {
                    // reached smallest possible depth (usually 1x1x1) so
                    // set value and return
                    _Value = newValue;
                    return;
                }
                else
                {
                    extent = _Size / 2;
                    byte byteExtent = (byte)extent;

                    _IsUniform = false;
                    _Nodes = new[]
                    {
                        new OctreeNode(byteExtent, _Value),
                        new OctreeNode(byteExtent, _Value),
                        new OctreeNode(byteExtent, _Value),
                        new OctreeNode(byteExtent, _Value),
                        new OctreeNode(byteExtent, _Value),
                        new OctreeNode(byteExtent, _Value),
                        new OctreeNode(byteExtent, _Value),
                        new OctreeNode(byteExtent, _Value)
                    };
                }
            }
            else
            {
                extent = _Size / 2;
            }

            DetermineOctant(x, y, z, extent, out float x0, out float y0, out float z0, out int octant);

            float floatExtent = extent;

            // recursively dig into octree and set
            _Nodes[octant].SetPoint(x - (x0 * floatExtent), y - (y0 * floatExtent), z - (z0 * floatExtent), newValue);

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

            ushort firstValue = _Nodes[0]._Value;

            // avoiding using linq here for performance sensitivity
            for (int index = 0; index < _Nodes.Length /* octants! */; index++)
            {
                OctreeNode node = _Nodes[index];

                if (!node._IsUniform || (node._Value.GetHashCode() != firstValue.GetHashCode()))
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
                int3 coords = WydMath.IndexTo3D(index, _Size);

                yield return GetPoint(coords.x, coords.y, coords.z);
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
        private static void DetermineOctant(float x, float y, float z, int extent, out float x0, out float y0, out float z0, out int octant)
        {
            x0 = y0 = z0 = 1f;
            octant = 7;

            if (x < extent)
            {
                x0 = 0f;
                octant -= 1;
            }

            if (y < extent)
            {
                y0 = 0f;
                octant -= 4;
            }

            if (z < extent)
            {
                z0 = 0f;
                octant -= 2;
            }
        }

        #endregion
    }
}
