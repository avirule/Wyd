using System;
using UnityEngine;
using Wyd.System;

namespace Wyd.Game.World.Chunks
{
    public class Octree
    {
        public static Vector3 MaximumSize = 16f * Vector3.one;

        private readonly OctreeNode _Origin;

        public Octree()
        {
            _Origin = new OctreeNode(new Bounds(MaximumSize / 2f, MaximumSize), 0);
        }

        public ushort GetPoint(Vector3 point)
        {
            // for easy maths, start 0,0,0 at 1,1,1
            point = point.Floor() + Vector3.one;

            if (!_Origin.Extents.Contains(point))
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }

            float depth = 16f;

            OctreeNode currentNode = _Origin;

            while ((!currentNode.Extents.Contains(point) || !currentNode.IsUniform) && depth > 1f)
            {
                Vector3 nextCoordinate = 16.DivideBy(point.FloorBy(8)) - Vector3.one;

                currentNode = currentNode.Nodes[nextCoordinate];
                depth /= 2f;
            }

            if (depth < 1f)
            {
                // failed
            }

            return currentNode.Id;
        }
    }
}
