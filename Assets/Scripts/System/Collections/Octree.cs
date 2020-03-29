#region

using System;
using UnityEngine;

#endregion

namespace Wyd.System.Collections
{
    public class Octree<T> where T : IEquatable<T>
    {
        private readonly OctreeNode<T> _Origin;

        public Octree(Vector3 centerPoint, float extent, T value) =>
            _Origin = new OctreeNode<T>(centerPoint, extent, value);

        public T GetPoint(Vector3 point) => _Origin.GetPoint(point);

        public void SetPoint(Vector3 point, T newValue)
        {
            _Origin.SetPoint(point, newValue);
        }
    }
}
