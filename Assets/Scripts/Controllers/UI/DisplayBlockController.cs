#region

using System.Collections.Generic;
using Controllers.State;
using Controllers.World;
using Game;
using Game.World.Blocks;
using UnityEngine;

#endregion

namespace Controllers.UI
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DisplayBlockController : MonoBehaviour
    {
        private MeshFilter _MeshFilter;
        private List<Vector3> _Vertices;
        private List<int> _Triangles;
        private List<Vector3> _UVs;
        private Mesh _Mesh;

        public ushort BlockId { get; private set; }

        private void Awake()
        {
            transform.localPosition = Vector3.zero;

            _Vertices = new List<Vector3>();
            _Triangles = new List<int>();
            _UVs = new List<Vector3>();

            _MeshFilter = GetComponent<MeshFilter>();
            _Mesh = _MeshFilter.sharedMesh;
        }

        private void Start()
        {
            GetComponent<MeshRenderer>().material = WorldController.Current.TerrainMaterial;
        }

        public void InitializeAs(ushort blockId)
        {
            BlockId = blockId;

            //_Vertices.Clear();
            //_Triangles.Clear();
            _UVs.Clear();

            // todo make this work with special block forms

            AddTriangles(Direction.Up);
            AddVertices(Direction.Up, Vector3.zero);
            if (BlockController.Current.GetBlockSpriteUVs(BlockId, Vector3.positiveInfinity, Direction.Up, Vector3.one,
                out Vector3[] uvs))
            {
                _UVs.AddRange(uvs);
            }

            AddTriangles(Direction.North);
            AddVertices(Direction.North, Vector3.zero);
            if (BlockController.Current.GetBlockSpriteUVs(BlockId, Vector3.positiveInfinity, Direction.North,
                Vector3.one, out uvs))
            {
                _UVs.AddRange(uvs);
            }

            AddTriangles(Direction.East);
            AddVertices(Direction.East, Vector3.zero);
            if (BlockController.Current.GetBlockSpriteUVs(BlockId, Vector3.positiveInfinity, Direction.East,
                Vector3.one, out uvs))
            {
                _UVs.AddRange(uvs);
            }

            //_Mesh.SetVertices(_Vertices);
            //_Mesh.SetTriangles(_Triangles, 0);
            _Mesh.SetUVs(0, _UVs);
        }

        private void AddTriangles(Direction direction)
        {
            int[] triangles = BlockFaces.Triangles.FaceTriangles[direction];

            foreach (int triangleValue in triangles)
            {
                _Triangles.Add(_Vertices.Count + triangleValue);
            }
        }

        private void AddVertices(Direction direction, Vector3 localPosition)
        {
            Vector3[] vertices = BlockFaces.Vertices.FaceVertices[direction];

            foreach (Vector3 vertex in vertices)
            {
                _Vertices.Add(vertex + localPosition);
            }
        }
    }
}
