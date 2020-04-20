#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMeshData
    {
        public List<Vector3> Vertices { get; }
        public List<int> Triangles { get; }
        public List<int> TransparentTriangles { get; }
        public List<Vector3> UVs { get; }
        public bool Empty { get; }

        public ChunkMeshData(List<Vector3> vertices, List<int> triangles, List<int> transparentTriangles,
            List<Vector3> uVs)
        {
            Vertices = vertices;
            Triangles = triangles;
            TransparentTriangles = transparentTriangles;
            UVs = uVs;
            Empty = Vertices.Count == 0 && Triangles.Count == 0 && TransparentTriangles.Count == 0 & UVs.Count == 0;
        }
    }
}
