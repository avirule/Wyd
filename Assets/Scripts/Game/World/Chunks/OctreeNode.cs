#region

using System.Collections.Generic;
using UnityEngine;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class OctreeNode
    {
        private static readonly Vector3[] _Coordinates =
        {
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1),

            new Vector3(1, 1, 1),
            new Vector3(-1, 1, 1),
            new Vector3(1, -1, 1),
            new Vector3(1, 1, -1)
        };

        public Dictionary<Vector3, OctreeNode> Nodes;

        public Bounds Extents { get; }
        public float Depth { get; }
        public ushort Id { get; private set; }
        public bool IsUniform => Nodes == null;

        public OctreeNode(Bounds extents, float depth, ushort id = 0)
        {
            Extents = extents;
            Depth = depth;
            Id = id;
        }

        public void Populate()
        {
            Vector3 centerOffset = Extents.center / 2f;

            Nodes = new Dictionary<Vector3, OctreeNode>
            {
                {
                    _Coordinates[0], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[0].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                },
                {
                    _Coordinates[1], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[1].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                },
                {
                    _Coordinates[2], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[2].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                },
                {
                    _Coordinates[3], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[3].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                },
                {
                    _Coordinates[4], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[4].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                },
                {
                    _Coordinates[5], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[5].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                },
                {
                    _Coordinates[6], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[6].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                },
                {
                    _Coordinates[7], new OctreeNode(new Bounds(
                        Extents.center + _Coordinates[7].MultiplyBy(centerOffset),
                        Depth.AsVector3()), Depth / 2f, Id)
                }
            };
        }
    }
}
