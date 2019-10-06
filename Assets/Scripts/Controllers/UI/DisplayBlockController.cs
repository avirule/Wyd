#region

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game;
using Wyd.Game.Entities.Inventory;
using Wyd.Game.World.Blocks;

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
            MeshRenderer.material = WorldController.Current.TerrainMaterial;
        }

        public void InitializeAs(ItemStack itemStack)
        {
            BlockId = itemStack.BlockId;

            _Vertices.Clear();
            _Triangles.Clear();
            _UVs.Clear();

            // todo make this work with special block forms

            AddTriangles(Direction.Up);
            AddVertices(Direction.Up, Vector3.zero);
            if (BlockController.Current.GetBlockSpriteUVs(BlockId, Vector3.positiveInfinity, Direction.Up, Vector3.one,
                out Vector3[] uvs))
            {
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[2]);
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[3]);
            }

            AddTriangles(Direction.North);
            AddVertices(Direction.North, Vector3.zero);
            if (BlockController.Current.GetBlockSpriteUVs(BlockId, Vector3.positiveInfinity, Direction.North,
                Vector3.one, out uvs))
            {
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[3]);
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[2]);
            }

            AddTriangles(Direction.East);
            AddVertices(Direction.East, Vector3.zero);
            if (BlockController.Current.GetBlockSpriteUVs(BlockId, Vector3.positiveInfinity, Direction.East,
                Vector3.one, out uvs))
            {
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[2]);
                _UVs.Add(uvs[3]);
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
