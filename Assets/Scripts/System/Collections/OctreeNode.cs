#region

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode<T> where T : unmanaged
    {
        private static readonly float3[] _coordinates =
        {
            new float3(-1, -1, -1),
            new float3(1, -1, -1),
            new float3(-1, -1, 1),
            new float3(1, -1, 1),

            new float3(-1, 1, -1),
            new float3(1, 1, -1),
            new float3(-1, 1, 1),
            new float3(1, 1, 1)
        };


        private readonly float _Size;

        private OctreeNode<T>[] _Nodes;

        public T Value { get; private set; }

        public bool IsUniform => _Nodes == null;

        public OctreeNode(float size, T value)
        {
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

        private void Populate()
        {
            float extent = _Size / 2f;

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


        #region Checked Data Operations

        public T GetPoint(float3 point) => !IsUniform ? _Nodes[DetermineOctant(point)].GetPoint(point) : Value;

        public void SetPoint(float3 point, T newValue)
        {
            if (Value.GetHashCode() == newValue.GetHashCode())
            {
                // operation does nothing, so return
                return;
            }

            if (_Size <= 1f)
            {
                // reached smallest possible depth (usually 1x1x1) so
                // set value and return
                Value = newValue;
                return;
            }

            if (IsUniform)
            {
                // node has no child nodes to traverse, so populate
                Populate();
            }

            // recursively dig into octree and set
            _Nodes[DetermineOctant(point)].SetPoint(point, newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (!IsUniform && CheckShouldCollapse())
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
                yield return GetPoint(WydMath.IndexTo3D(index, (int)_Size));
            }
        }

        #endregion


        #region Try .. Data Operations

        public bool TryGetPoint(float3 point, out T value)
        {
            value = default;

            if (!IsUniform)
            {
                int octant = DetermineOctant(point);
                return _Nodes[octant].TryGetPoint(point, out value);
            }
            else
            {
                value = Value;
                return true;
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
        private int DetermineOctant(float3 point)
        {
            bool3 result = point < (_Size / 2f);
            return (result[0] ? 0 : 1) + (result[1] ? 0 : 4) + (result[2] ? 0 : 2);
        }

        #endregion
    }
}
