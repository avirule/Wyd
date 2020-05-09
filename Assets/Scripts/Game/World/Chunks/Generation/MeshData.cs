#region

using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public class MeshData
    {
        private readonly List<Vector3> _Vertices;
        private readonly List<List<uint>> _Triangles;
        private readonly List<Vector3> _UVs;

        public int VerticesCount => _Vertices.Count;

        public bool Empty => (_Vertices.Count == 0)
                             && (_UVs.Count == 0)
                             && ((_Triangles.Count == 0) || _Triangles.All(triangles => triangles.Count == 0));

        private MeshData()
        {
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Triangles = new List<List<uint>>();
        }

        public MeshData(List<Vector3> vertices, List<Vector3> uvs, params List<uint>[] triangles) : this()
        {
            _Vertices = vertices;
            _UVs = uvs;
            _Triangles.InsertRange(0, triangles);
        }

        public void Clear(bool trimExcess = false)
        {
            _Vertices.Clear();
            _UVs.Clear();

            if (trimExcess)
            {
                _Vertices.TrimExcess();
                _UVs.TrimExcess();
            }

            foreach (List<uint> triangles in _Triangles)
            {
                triangles.Clear();

                if (trimExcess)
                {
                    triangles.TrimExcess();
                }
            }
        }

        public void AddVertex(float3 vertex) => _Vertices.Add(vertex);
        public void AddUV(float3 uv) => _UVs.Add(uv);
        public void AddTriangle(int subMesh, uint triangle) => _Triangles[subMesh].Add(triangle);
        public void AddTriangles(int subMesh, params uint[] triangles) => _Triangles[subMesh].AddRange(triangles);
        public void AddTriangles(int subMesh, IEnumerable<uint> triangles) => _Triangles[subMesh].AddRange(triangles);

        public void ApplyMeshData(ref Mesh mesh)
        {
            // 'is object' to bypass unity lifetime check for null
            if (mesh is object)
            {
                mesh = new Mesh();
            }
            else
            {
                mesh.Clear();
            }

            mesh.subMeshCount = _Triangles.Count;

            // mesh.SetVertexBufferParams(_Vertices.Count, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.SInt32, 1));
            // mesh.SetVertexBufferData(_Vertices, 0, 0, _Vertices.Count, 0);
            //
            // foreach (List<uint> triangles in _Triangles.Where(triangles => triangles.Count != 0))
            // {
            //     mesh.SetIndexBufferParams(triangles.Count, IndexFormat.UInt32);
            //     mesh.SetIndexBufferData(triangles, 0, 0, triangles.Count);
            // }

            // check uvs count in case of no UVs to apply to mesh
            // if (_UVs.Count > 0)
            // {
            //     mesh.SetUVs(0, _UVs);
            // }

            // mesh.RecalculateNormals();
        }
    }
}
