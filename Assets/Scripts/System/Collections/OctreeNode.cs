// ReSharper disable ConvertToAutoPropertyWithPrivateSetter

#region

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode
    {
        #region Instance Members

        private OctreeNode[] _Nodes;

        public bool IsUniform => _Nodes == null;

        public ushort Value { get; set; }

        public OctreeNode this[int index] => _Nodes[index];

        #endregion

        /// <summary>
        ///     Creates an in-memory compressed 3D representation of any unmanaged data type.
        /// </summary>
        /// <param name="value">Initial value of the collection.</param>
        public OctreeNode(ushort value) => Value = value;


        #region Data Operations

        public ushort GetPoint(float extent, float x, float y, float z)
        {
            if (IsUniform)
            {
                return Value;
            }

            Octree.DetermineOctant(extent, ref x, ref y, ref z, out int octant);

            return _Nodes[octant].GetPoint(extent / 2f, x, y, z);
        }

        public void SetPoint(float extent, float x, float y, float z, ushort newValue)
        {
            if (IsUniform)
            {
                if (Value == newValue)
                {
                    return;
                }
                else if (extent < 1f)
                {
                    // reached smallest possible depth (usually 1x1x1) so
                    // set value and return
                    Value = newValue;
                    return;
                }
                else
                {
                    Populate();
                }
            }

            Octree.DetermineOctant(extent, ref x, ref y, ref z, out int octant);

            // recursively dig into octree and set
            _Nodes[octant].SetPoint(extent / 2f, x, y, z, newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (CheckShouldCollapse())
            {
                Collapse();
            }
        }

        #endregion


        #region Helper Methods

        public void Populate()
        {
            _Nodes = new[]
            {
                new OctreeNode(Value),
                new OctreeNode(Value),
                new OctreeNode(Value),
                new OctreeNode(Value),
                new OctreeNode(Value),
                new OctreeNode(Value),
                new OctreeNode(Value),
                new OctreeNode(Value)
            };
        }

        public void PopulateRecursive(float extent)
        {
            if (extent <= 1f)
            {
                return;
            }

            extent /= 2f;

            Populate();

            foreach (OctreeNode octreeNode in _Nodes)
            {
                octreeNode.PopulateRecursive(extent);
            }
        }

        public void Collapse()
        {
            Value = _Nodes[0].Value;
            _Nodes = null;
        }

        public void CollapseRecursive()
        {
            if (IsUniform)
            {
                return;
            }

            foreach (OctreeNode octreeNode in _Nodes)
            {
                octreeNode.CollapseRecursive();
            }

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
            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (OctreeNode octreeNode in _Nodes)
            {
                if (!octreeNode.IsUniform || (octreeNode.Value != firstValue))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
