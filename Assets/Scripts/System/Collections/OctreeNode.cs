#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode<T> where T : IEquatable<T>
    {
        private static readonly float3[] _Coordinates =
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

        public List<OctreeNode<T>> Nodes;

        public Volume Volume { get; }
        public T Value { get; private set; }
        public bool IsUniform => Nodes == null;

        public OctreeNode(float3 centerPoint, float extent, T value)
        {
            Volume = new Volume(centerPoint, new float3(extent));
            Value = value;
        }

        public void Collapse()
        {
            Nodes = null;
        }

        public void Populate()
        {
            float centerOffsetValue = Volume.Extents.x / 2f;
            float3 centerOffset = new float3(centerOffsetValue);

            Nodes = new List<OctreeNode<T>>
            {
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[0] * centerOffset), Volume.Extents.x, Value),
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[1] * centerOffset), Volume.Extents.x, Value),
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[2] * centerOffset), Volume.Extents.x, Value),
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[3] * centerOffset), Volume.Extents.x, Value),
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[4] * centerOffset), Volume.Extents.x, Value),
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[5] * centerOffset), Volume.Extents.x, Value),
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[6] * centerOffset), Volume.Extents.x, Value),
                new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[7] * centerOffset), Volume.Extents.x, Value)
            };
        }

        public T GetPoint(float3 point)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(
                    $"Cannot get point: specified point {point} not contained within bounds {Volume}.");
            }

            return IsUniform ? Value : Nodes[DetermineOctant(point)].GetPoint(point);
        }

        public void SetPoint(float3 point, T newValue)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(
                    $"Cannot set point: specified point {point} not contained within bounds {Volume}.");
            }

            if (IsUniform && Value.Equals(newValue))
            {
                // operation does nothing, so return
                return;
            }

            if (Volume.Size.x <= 1f)
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

            int octant = DetermineOctant(point);

            // recursively dig into octree
            Nodes[octant].SetPoint(point, newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse child nodes if all are equal
            if (!CheckShouldCollapse())
            {
                return;
            }

            Value = Nodes[0].Value;
            Collapse();
        }

        private bool CheckShouldCollapse()
        {
            if (Nodes == null)
            {
                return false;
            }

            if (Nodes.Count == 0)
            {
                return true;
            }

            T firstValue = Nodes[0].Value;
            return Nodes.All(node => node.IsUniform) && Nodes.All(node => node.Value.Equals(firstValue));
        }


        #region HELPER METHODS

        public bool ContainsMinBiased(float3 point) =>
            (point.x < Volume.MaxPoint.x)
            && (point.y < Volume.MaxPoint.y)
            && (point.z < Volume.MaxPoint.z)
            && (point.x >= Volume.MinPoint.x)
            && (point.y >= Volume.MinPoint.y)
            && (point.z >= Volume.MinPoint.z);

        // indexes:
        // bottom half quadrant:
        // 1 3
        // 0 2
        // top half quadrant:
        // 5 7
        // 4 6
        private int DetermineOctant(float3 point) =>
            (point.x < Volume.CenterPoint.x ? 0 : 1)
            + (point.y < Volume.CenterPoint.y ? 0 : 4)
            + (point.z < Volume.CenterPoint.z ? 0 : 2);

        #endregion
    }
}
