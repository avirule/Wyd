#region

using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Game;
using Wyd.Game.Entities.Inventory;
using Wyd.Game.World.Blocks;
using Wyd.Graphics;

#endregion

namespace Wyd.Controllers.UI
{
    public class DisplayBlockController : MonoBehaviour
    {
        private List<Vector3> _Vertices;
        private List<int> _Triangles;
        private List<Vector3> _UVs;
        private Mesh _Mesh;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public TextMeshProUGUI AmountText;

        public ushort BlockId { get; private set; }
        public int Amount { get; private set; }

        private void Awake()
        {
            transform.localPosition = Vector3.zero;

            _Vertices = new List<Vector3>();
            _Triangles = new List<int>();
            _UVs = new List<Vector3>();
            _Mesh = new Mesh();

            MeshFilter.sharedMesh = _Mesh;
        }

        private void Start()
        {
            MeshRenderer.material = TextureController.Current.TerrainMaterials[0];
        }

        public void InitializeAs(ItemStack itemStack)
        {
            BlockId = itemStack.BlockId;

            _Vertices.Clear();
            _Triangles.Clear();
            _UVs.Clear();

            AddTriangles(Direction.Up);
            AddVertices(Direction.Up, Vector3.zero);
            if (BlockController.Current.GetUVs(BlockId, int3.zero, Direction.Up, new float3(1f),
                out BlockUVs blockUVs))
            {
                _UVs.Add(blockUVs[0]);
                _UVs.Add(blockUVs[2]);
                _UVs.Add(blockUVs[1]);
                _UVs.Add(blockUVs[3]);
            }

            AddTriangles(Direction.North);
            AddVertices(Direction.North, Vector3.zero);
            if (BlockController.Current.GetUVs(BlockId, int3.zero, Direction.North, new float3(1f),
                out blockUVs))
            {
                _UVs.Add(blockUVs[1]);
                _UVs.Add(blockUVs[3]);
                _UVs.Add(blockUVs[0]);
                _UVs.Add(blockUVs[2]);
            }

            AddTriangles(Direction.East);
            AddVertices(Direction.East, Vector3.zero);
            if (BlockController.Current.GetUVs(BlockId, int3.zero, Direction.East, new float3(1f),
                out blockUVs))
            {
                _UVs.Add(blockUVs[0]);
                _UVs.Add(blockUVs[1]);
                _UVs.Add(blockUVs[2]);
                _UVs.Add(blockUVs[3]);
            }

            _Mesh.SetVertices(_Vertices);
            _Mesh.SetTriangles(_Triangles, 0);
            _Mesh.SetUVs(0, _UVs);

            AmountText.text = itemStack.Amount.ToString();
        }

        private void AddTriangles(Direction direction)
        {
            int[] triangles = BlockFaces.Triangles.FaceTriangles[direction];

            foreach (int triangleValue in triangles)
            {
                _Triangles.Add(_Vertices.Count + triangleValue);
            }
        }

        private void AddVertices(Direction direction, float3 localPosition)
        {
            float3[] vertices = BlockFaces.Vertices.FaceVertices[direction];

            foreach (float3 vertex in vertices)
            {
                _Vertices.Add(vertex + localPosition);
            }
        }
    }
}
