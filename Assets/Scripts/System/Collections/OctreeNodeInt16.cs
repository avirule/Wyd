// ReSharper disable ConvertToAutoPropertyWithPrivateSetter

using System;

namespace Wyd.System.Collections
{
    public class OctreeNode
    {
        #region Instance Members

        private OctreeNode[] _Nodes;
        private ushort _Value;

        public ushort Value => _Value;
        public bool IsUniform => _Nodes == null;

        #endregion

        /// <summary>
        ///     Creates an in-memory compressed 3D representation of any unmanaged data type.
        /// </summary>
        /// <param name="value">Initial value of the collection.</param>
        public OctreeNode(ushort value) => _Value = value;


        #region Data Operations

        public ushort GetPoint(float extent, float x, float y, float z)
        {
            if (IsUniform)
            {
                return _Value;
            }

            DetermineOctant(extent, x, y, z, out float x0, out float y0, out float z0, out int octant);

            return _Nodes[octant].GetPoint(extent / 2f, x - (x0 * extent), y - (y0 * extent), z - (z0 * extent));
        }

        public void SetPoint(float extent, float x, float y, float z, ushort newValue)
        {
            if (IsUniform)
            {
                if (_Value == newValue)
                {
                    return;
                }
                else if (extent < 1f)
                {
                    // reached smallest possible depth (usually 1x1x1) so
                    // set value and return
                    _Value = newValue;
                    return;
                }
                else
                {
                    _Nodes = new[]
                    {
                        new OctreeNode(_Value),
                        new OctreeNode(_Value),
                        new OctreeNode(_Value),
                        new OctreeNode(_Value),
                        new OctreeNode(_Value),
                        new OctreeNode(_Value),
                        new OctreeNode(_Value),
                        new OctreeNode(_Value)
                    };
                }
            }

            DetermineOctant(extent, x, y, z, out float x0, out float y0, out float z0, out int octant);

            // recursively dig into octree and set
            _Nodes[octant].SetPoint(extent / 2f, x - (x0 * extent), y - (y0 * extent), z - (z0 * extent), newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (CheckShouldCollapse())
            {
                _Value = _Nodes[0]._Value;
                _Nodes = null;
            }
        }

        private bool CheckShouldCollapse()
        {
            if (IsUniform)
            {
                return false;
            }

            ushort firstValue = _Nodes[0]._Value;

            // avoiding using linq here for performance sensitivity
            for (int index = 0; index < _Nodes.Length /* octants! */; index++)
            {
                OctreeNode node = _Nodes[index];

                if (!node.IsUniform || (node._Value != firstValue))
                {
                    return false;
                }
            }

            return true;
        }

        // public IEnumerable<ushort> GetAllData()
        // {
        //     for (int index = 0; index < math.pow(_Size, 3); index++)
        //     {
        //         int3 coords = WydMath.IndexTo3D(index, _Size);
        //
        //         yield return GetPoint(coords.x, coords.y, coords.z);
        //     }
        // }

        #endregion


        #region Helper Methods

        // indexes:
        // bottom half quadrant indexes:
        // 1 3
        // 0 2
        // top half quadrant indexes:
        // 5 7
        // 4 6
        private static void DetermineOctant(float extent, float x, float y, float z, out float x0, out float y0, out float z0, out int octant)
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
