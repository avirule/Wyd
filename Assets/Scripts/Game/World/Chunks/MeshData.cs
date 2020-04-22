#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class MeshData
    {
        private readonly List<Vector3> _Vertices;
        private readonly List<List<int>> _Triangles;
        private readonly List<Vector3> _UVs;

        public IReadOnlyList<Vector3> Vertices => _Vertices;
        public IReadOnlyList<Vector3> UVs => _UVs;
        public IReadOnlyList<IReadOnlyList<int>> Triangles => _Triangles;

        public bool Empty => (_Vertices.Count == 0)
                             && (_UVs.Count == 0)
                             && ((_Triangles.Count == 0) || _Triangles.All(triangles => triangles.Count == 0));

        public MeshData()
        {
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Triangles = new List<List<int>>();
        }

        public MeshData(List<Vector3> vertices, List<Vector3> uvs, params List<int>[] triangles) : this()
        {
            _Vertices = vertices;
            _UVs = uvs;
            _Triangles.InsertRange(0, triangles);
        }

        public void Clear()
        {
            _Vertices.Clear();
            _UVs.Clear();

            foreach (List<int> triangles in _Triangles)
            {
                triangles.Clear();
            }
        }

        public void AddVertex(Vector3 vertex) => _Vertices.Add(vertex);
        public void AddUV(Vector3 uv) => _UVs.Add(uv);
        public void AddTriangles(int subMesh, params int[] triangles) => _Triangles[subMesh].AddRange(triangles);
        public void AddTriangles(int subMesh, IEnumerable<int> triangles) => _Triangles[subMesh].AddRange(triangles);

        public void ApplyMeshData(ref Mesh mesh)
        {
            if (mesh == null)
            {
                mesh = new Mesh();
            }
            else
            {
                mesh.Clear();
            }

            mesh.subMeshCount = 2;
            mesh.indexFormat = _Vertices.Count > 65000
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            mesh.SetVertices(_Vertices);

            for (int index = 0; index < _Triangles.Count; index++)
            {
                if (_Triangles[index].Count == 0)
                {
                    continue;
                }

                mesh.SetTriangles(_Triangles[index], index);
            }

            // check uvs count in case of no UVs to apply to mesh
            if (_UVs.Count > 0)
            {
                mesh.SetUVs(0, _UVs);
            }

            mesh.RecalculateNormals();
        }
    }
}
