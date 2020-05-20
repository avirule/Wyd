#region

using System;
using System.Threading;

#endregion

namespace Wyd.World.Chunks.Generation
{
    public class MeshData
    {
        private readonly Action<int[], int[]> _ReleaseResourcesAction;
        public int[] Vertexes { get; }
        public int VertexesCount { get; set; }
        public int[] Triangles { get; }
        public int TrianglesCount { get; set; }

        public MeshData(Action<int[], int[]> releaseResourcesAction, int[] vertexes, int[] triangles) =>
            (_ReleaseResourcesAction, Vertexes, VertexesCount, Triangles, TrianglesCount) = (releaseResourcesAction, vertexes, 0, triangles, 0);

        public void Release() => _ReleaseResourcesAction.Invoke(Vertexes, Triangles);
    }
}
