#region

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Game.World.Chunks
{
    public class MeshData
    {
        public List<Vector3> Vertices { get; }
        public List<int> Triangles { get; }
        public List<int> TransparentTriangles { get; }
        public List<Vector3> UVs { get; }
        public List<Color32> Colors { get; }

        public void Clear()
        {
            Vertices.Clear();
            Triangles.Clear();
            UVs.Clear();
            Colors.Clear();
        }

        public MeshData()
        {
            Vertices = UVs = new List<Vector3>();
            Triangles = TransparentTriangles = new List<int>();
            Colors = new List<Color32>();
        }

        public void AllocateMeshData(MeshData meshData)
        {
            Vertices.AddRange(meshData.Vertices);
            Triangles.AddRange(meshData.Triangles);
            TransparentTriangles.AddRange(meshData.TransparentTriangles);
            UVs.AddRange(meshData.UVs);
            Colors.AddRange(meshData.Colors);
        }

        public void ApplyToMesh(ref Mesh mesh, bool immediateUpload = false, bool readOnlyData = false)
        {
            if ((Vertices.Count == 0) || ((Triangles.Count == 0) && (TransparentTriangles.Count == 0)))
            {
                return;
            }

            mesh.Clear();

            mesh.subMeshCount = 2;
            mesh.indexFormat = Vertices.Count > 65000
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            mesh.MarkDynamic();
            mesh.SetVertices(Vertices);
            mesh.SetTriangles(Triangles, 0);
            mesh.SetTriangles(TransparentTriangles, 1);
            mesh.SetColors(Colors);

            // check uvs count in case of no UVs to apply to mesh
            if (UVs.Count > 0)
            {
                mesh.SetUVs(0, UVs);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            if (immediateUpload)
            {
                mesh.UploadMeshData(readOnlyData);
            }
        }
    }
}
