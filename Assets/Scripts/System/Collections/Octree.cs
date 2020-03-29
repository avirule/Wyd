#region

using System;
using UnityEngine;

#endregion

namespace Wyd.System.Collections
{
    public class Octree<T> where T : IEquatable<T>
    {
        private readonly OctreeNode<T> Origin;

        public Octree(Vector3 centerPoint, float extent, T value) => Origin = new OctreeNode<T>(centerPoint, extent, value);

        public T GetPoint(Vector3 point)
        {
            return Origin.GetPoint(point);
        }

        public void SetPoint(Vector3 point, T newValue)
        {
            Origin.SetPoint(point, newValue);
        }
    }
}
