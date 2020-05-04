// ReSharper disable ConvertToAutoPropertyWithPrivateSetter

#region

#endregion

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

        public OctreeNode this[int index] => _Nodes[index];

        #endregion

        /// <summary>
        ///     Creates an in-memory compressed 3D representation of any unmanaged data type.
        /// </summary>
        /// <param name="value">Initial value of the collection.</param>
        public OctreeNode(ushort value)
        {
            _Value = value;
        }


        #region Data Operations

        public ushort GetPoint(float extent, float x, float y, float z)
        {
            if (IsUniform)
            {
                return _Value;
            }

            Octree.DetermineOctant(extent, x, y, z, out float x0, out float y0, out float z0, out int octant);

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

            Octree.DetermineOctant(extent, x, y, z, out float x0, out float y0, out float z0, out int octant);

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

        #endregion


        #region Helper Methods

        private bool CheckShouldCollapse()
        {
            if (IsUniform)
            {
                return false;
            }

            ushort firstValue = _Nodes[0]._Value;

            // avoiding using linq here for performance sensitivity
            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (int index = 0; index < _Nodes.Length; index++)
            {
                OctreeNode node = _Nodes[index];

                if (!node.IsUniform || (node._Value != firstValue))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
