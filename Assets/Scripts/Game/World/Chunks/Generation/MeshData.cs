#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public class MeshData
    {
        private readonly List<int> _Vertices;
        private readonly List<List<int>> _Triangles;
        private readonly List<Vector3> _UVs;

        public int VerticesCount => _Vertices.Count;

        public bool Empty => (_Vertices.Count == 0)
                             && (_UVs.Count == 0)
                             && ((_Triangles.Count == 0) || _Triangles.All(triangles => triangles.Count == 0));

        private MeshData()
        {
            _Vertices = new List<int>();
            _UVs = new List<Vector3>();
            _Triangles = new List<List<int>>();
        }

        public MeshData(List<int> vertices, List<Vector3> uvs, params List<int>[] triangles) : this()
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

            foreach (List<int> triangles in _Triangles)
            {
                triangles.Clear();

                if (trimExcess)
                {
                    triangles.TrimExcess();
                }
            }
        }

        public void AddVertex(int compressedVertex) => _Vertices.Add(compressedVertex);
        public void AddUV(Vector3 uv) => _UVs.Add(uv);
        public void AddTriangle(int subMesh, int triangle) => _Triangles[subMesh].Add(triangle);
        public void AddTriangles(int subMesh, params int[] triangles) => _Triangles[subMesh].AddRange(triangles);
        public void AddTriangles(int subMesh, IEnumerable<int> triangles) => _Triangles[subMesh].AddRange(triangles);

        public void ApplyMeshData(ref Mesh mesh)
        {
            // 'is object' to bypass unity lifetime check for null
            if (!(mesh is object))
            {
                throw new NullReferenceException(nameof(mesh));
            }
            else
            {
                mesh.Clear();
            }

            const MeshUpdateFlags default_flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

            mesh.subMeshCount = _Triangles.Count;

            mesh.SetVertexBufferParams(_Vertices.Count, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.SInt32, 1));
            mesh.SetVertexBufferData(_Vertices, 0, 0, _Vertices.Count, 0, default_flags);

            if (_UVs.Count > 0)
            {
                // mesh.SetVertexBufferParams(_UVs.Count, new VertexAttributeDescriptor(VertexAttribute.TexCoord0));
                // mesh.SetVertexBufferData(_UVs, 0, 0, _UVs.Count, 0, default_flags);

                mesh.SetUVs(0, _UVs);
            }

            foreach (List<int> triangles in _Triangles.Where(triangles => triangles.Count != 0))
            {
                mesh.SetIndexBufferParams(triangles.Count, IndexFormat.UInt32);
                mesh.SetIndexBufferData(triangles, 0, 0, triangles.Count, default_flags);
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Count), default_flags);
            }

            mesh.bounds = new Bounds(new float3(GenerationConstants.CHUNK_SIZE / 2), new float3(GenerationConstants.CHUNK_SIZE));
        }
    }
}
