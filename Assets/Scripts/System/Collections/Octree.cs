#region

using System;
using System.Collections.Generic;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class Octree<T> where T : unmanaged
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
            if (collapse && !_Origin.IsUniform)
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
            for (int index = 0; index < WydMath.Product(_Origin.Volume.Size); index++)
            {
                yield return GetPoint(_Origin.Volume.MinPoint
                                      + WydMath.IndexTo3D(index, WydMath.ToInt(_Origin.Volume.Size)));
            }
        }
    }
}
