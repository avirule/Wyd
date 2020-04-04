#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

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

        public Bounds Bounds { get; }
        public T Value { get; private set; }
        public bool IsUniform => Nodes == null;

        public OctreeNode(float3 centerPoint, float extent, T value)
        {
            Bounds = new Bounds(centerPoint, new float3(extent));
            Value = value;
        }

        public void Collapse()
        {
            Nodes = null;
        }

        public void Populate()
        {
            float newExtent = Bounds.Extents.x / 2f;
            float centerOffsetValue = newExtent / 2f;
            Vector3 centerOffset = new Vector3(centerOffsetValue, centerOffsetValue, centerOffsetValue);

            Nodes = new List<OctreeNode<T>>
            {
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[0] * centerOffset), newExtent, Value),
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[1] * centerOffset), newExtent, Value),
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[2] * centerOffset), newExtent, Value),
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[3] * centerOffset), newExtent, Value),
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[4] * centerOffset), newExtent, Value),
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[5] * centerOffset), newExtent, Value),
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[6] * centerOffset), newExtent, Value),
                new OctreeNode<T>(Bounds.CenterPoint + (_Coordinates[7] * centerOffset), newExtent, Value)
            };
        }

        public T GetPoint(float3 point)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }

            return IsUniform ? Value : Nodes[DetermineOctant(point)].GetPoint(point);
        }

        public void SetPoint(float3 point, T newValue)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }

            if (IsUniform && Value.Equals(newValue))
            {
                // operation does nothing, so return
                return;
            }

            if (Bounds.Extents.x <= 1f)
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
            T firstValue = Nodes[0].Value;
            return Nodes.All(node => node.IsUniform) && Nodes.All(node => node.Value.Equals(firstValue));
        }


        #region HELPER METHODS

        public bool ContainsMinBiased(Vector3 point) =>
            (point.x < Bounds.MaxPoint.x)
            && (point.y < Bounds.MaxPoint.y)
            && (point.z < Bounds.MaxPoint.z)
            && (point.x >= Bounds.MinPoint.x)
            && (point.y >= Bounds.MinPoint.y)
            && (point.z >= Bounds.MinPoint.z);

        // indexes:
        // bottom half quadrant:
        // 1 3
        // 0 2
        // top half quadrant:
        // 5 7
        // 4 6
        private int DetermineOctant(Vector3 point) =>
            (point.x < Bounds.CenterPoint.x ? 0 : 1)
            + (point.y < Bounds.CenterPoint.y ? 0 : 4)
            + (point.z < Bounds.CenterPoint.z ? 0 : 2);

        #endregion
    }
}
