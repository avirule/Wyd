#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Wyd.System.Collections
{
    public class Octree<T> where T : IEquatable<T>
    {
        private readonly OctreeNode<T> _Origin;

        public Octree(Vector3 centerPoint, float extent, T value) =>
            _Origin = new OctreeNode<T>(centerPoint, extent, value);

        public bool ContainsPoint(Vector3 point) => _Origin.ContainsMinBiased(point);

        public T GetPoint(Vector3 point) => _Origin.GetPoint(point);

        public void SetPoint(Vector3 point, T newValue)
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
            Vector3 size = new Vector3(_Origin.Extent, _Origin.Extent, _Origin.Extent);
            for (int index = 0; index < size.Product(); index++)
            {
                Vector3Int localPosition = WydMath.GetIndexAsVector3Int(index, size.AsVector3Int());
                Vector3 globalPosition = _Origin.MinPoint + localPosition;

                yield return GetPoint(globalPosition);
            }
        }
    }
}
