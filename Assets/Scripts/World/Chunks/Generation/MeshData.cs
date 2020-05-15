#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Wyd.World.Chunks.Generation
{
    public class MeshData
    {
        private static readonly VertexAttributeDescriptor[] _Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.SInt32, 1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.SInt32, 1)
        };

        private readonly List<int> _Vertices;
        private readonly List<List<int>> _Triangles;

        public int VerticesCount => _Vertices.Count;

        public bool Empty => (_Vertices.Count == 0)
                             && ((_Triangles.Count == 0) || _Triangles.All(triangles => triangles.Count == 0));

        private MeshData()
        {
            _Vertices = new List<int>();
            _Triangles = new List<List<int>>();
        }

        public MeshData(List<int> vertices, params List<int>[] triangles) : this()
        {
            _Vertices = vertices;
            _Triangles.InsertRange(0, triangles);
        }

        public void Clear(bool trimExcess = false)
        {
            _Vertices.Clear();

            if (trimExcess)
            {
                _Vertices.TrimExcess();
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

            if ((_Vertices.Count == 0) || (_Triangles.Count == 0))
            {
                return;
            }

            if ((int)((_Vertices.Count / 2f) * 1.5f) != _Triangles.Sum(triangles => triangles.Count))
            {
                throw new ArgumentOutOfRangeException($"Sum of all {_Triangles} should be 1.5x as many vertices.");
            }

            const MeshUpdateFlags default_flags = MeshUpdateFlags.DontRecalculateBounds
                                                  | MeshUpdateFlags.DontValidateIndices
                                                  | MeshUpdateFlags.DontResetBoneBounds;

            mesh.SetVertexBufferParams(_Vertices.Count, _Layout);
            mesh.SetVertexBufferData(_Vertices, 0, 0, _Vertices.Count, 0, default_flags);

            mesh.subMeshCount = _Triangles.Count;

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
