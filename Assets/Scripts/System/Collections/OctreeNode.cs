#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode<T> where T : IEquatable<T>
    {
        private static readonly Vector3[] _Coordinates =
        {
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(-1, -1, 1),
            new Vector3(1, -1, 1),

            new Vector3(-1, 1, -1),
            new Vector3(1, 1, -1),
            new Vector3(-1, 1, 1),
            new Vector3(1, 1, 1)
        };

        public List<OctreeNode<T>> Nodes;

        public Vector3 CenterPoint { get; }
        public Vector3 MaxPoint { get; }
        public Vector3 MinPoint { get; }
        public float Extent { get; }
        public T Value { get; private set; }
        public bool IsUniform => Nodes == null;

        public OctreeNode(Vector3 centerPoint, float extent, T value)
        {
            CenterPoint = centerPoint;
            Extent = extent;
            Value = value;

            // set min/max points
            Vector3 extents = new Vector3(Extent, Extent, Extent) / 2f;
            MaxPoint = CenterPoint + extents;
            MinPoint = CenterPoint - extents;
        }

        #region HELPER METHODS

        private bool ContainsMinBiased(Vector3 point) =>
            (point.x < MaxPoint.x)
            && (point.y < MaxPoint.y)
            && (point.z < MaxPoint.z)
            && (point.x >= MinPoint.x)
            && (point.y >= MinPoint.y)
            && (point.z >= MinPoint.z);

        // indexes:
        // bottom half quadrant:
        // 1 3
        // 0 2
        // top half quadrant:
        // 5 7
        // 4 6
        private int DetermineOctant(Vector3 point) =>
            (point.x < CenterPoint.x ? 0 : 1) + (point.y < CenterPoint.y ? 0 : 4) + (point.z < CenterPoint.z ? 0 : 2);

        #endregion

        public void Populate()
        {
            float newExtent = Extent / 2f;
            float centerOffsetValue = newExtent / 2f;
            Vector3 centerOffset = new Vector3(centerOffsetValue, centerOffsetValue, centerOffsetValue);

            Nodes = new List<OctreeNode<T>>
            {
                new OctreeNode<T>(CenterPoint + _Coordinates[0].MultiplyBy(centerOffset), newExtent, Value),
                new OctreeNode<T>(CenterPoint + _Coordinates[1].MultiplyBy(centerOffset), newExtent, Value),
                new OctreeNode<T>(CenterPoint + _Coordinates[2].MultiplyBy(centerOffset), newExtent, Value),
                new OctreeNode<T>(CenterPoint + _Coordinates[3].MultiplyBy(centerOffset), newExtent, Value),
                new OctreeNode<T>(CenterPoint + _Coordinates[4].MultiplyBy(centerOffset), newExtent, Value),
                new OctreeNode<T>(CenterPoint + _Coordinates[5].MultiplyBy(centerOffset), newExtent, Value),
                new OctreeNode<T>(CenterPoint + _Coordinates[6].MultiplyBy(centerOffset), newExtent, Value),
                new OctreeNode<T>(CenterPoint + _Coordinates[7].MultiplyBy(centerOffset), newExtent, Value)
            };
        }

        public T GetPoint(Vector3 point)
        {
            point = point.Floor();

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }

            return IsUniform ? Value : Nodes[DetermineOctant(point)].GetPoint(point);
        }

        public void SetPoint(Vector3 point, T newValue)
        {
            point = point.Floor();

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }

            if (IsUniform && Value.Equals(newValue))
            {
                // operation does nothing, so return
                return;
            }

            if (Extent <= 1f)
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
            if (CheckShouldCollapse())
            {
                Collapse();
            }
        }

        private bool CheckShouldCollapse()
        {
            return Nodes.All(node => node.IsUniform) && Nodes.All(node => node.Value.Equals(Value));
        }

        private void Collapse()
        {
            Nodes = null;
        }
    }
}
