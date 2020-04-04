#region

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

#endregion

namespace Wyd.System.Collections
{
    public class Octree<T> where T : IEquatable<T>
    {
        private readonly OctreeNode<T> _Origin;

        public Octree(float3 centerPoint, float extent, T value) =>
            _Origin = new OctreeNode<T>(centerPoint, extent, value);

        public bool ContainsPoint(float3 point) => _Origin.ContainsMinBiased(point);

        public T GetPoint(float3 point) => _Origin.GetPoint(point);

        public void SetPoint(float3 point, T newValue)
        {
            _Origin.SetPoint(point, newValue);
        }

        public void Collapse(bool collapse)
        {
            if (collapse)
            {
                _Origin.Collapse();
            }
        }

        public bool IsOriginNodeUniform(out T value)
        {
            value = _Origin.Value;
            return _Origin.IsUniform;
        }

        public IEnumerable<T> GetAllData()
        {
            int3 size = WydMath.ToInt(_Origin.Bounds.Extents);
            for (int index = 0; index < WydMath.Product(size); index++)
            {
                yield return GetPoint(_Origin.Bounds.MinPoint + WydMath.IndexTo3D(index, size));
            }
        }
    }
}
