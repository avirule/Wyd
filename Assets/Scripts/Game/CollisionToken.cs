#region

using System;
using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using Game.World.Blocks;
using UnityEngine;

#endregion

namespace Game
{
    public class CollisionToken : MonoBehaviour
    {
        private Transform _SelfTransform;
        private List<Vector3> _Vertices;
        private List<int> _Triangles;
        private bool _ScheduledRecalculation;

        public Transform AuthorTransform;
        private int _Radius;

        public Mesh Mesh { get; private set; }
        public Bounds BoundingBox { get; private set; }

        public int Radius
        {
            get => _Radius;
            set
            {
                if (_Radius == value)
                {
                    return;
                }

                _Radius = value;
                _ScheduledRecalculation = true;
            }
        }


        public event EventHandler<Mesh> UpdatedMesh;

        private void Awake()
        {
            _SelfTransform = transform;
            _Vertices = new List<Vector3>();
            _Triangles = new List<int>();

            AuthorTransform = _SelfTransform.parent;
        }

        private void Start()
        {
            WorldController.Current.ChunkMeshChanged += OnChunkMeshChangedInWorld;
        }

        private void Update()
        {
            if (AuthorTransform == default)
            {
                return;
            }

            Vector3 difference = (_SelfTransform.position - AuthorTransform.position).Abs();

            if (_ScheduledRecalculation || difference.AnyGreaterThanOrEqual(Vector3.one))
            {
                _SelfTransform.position = AuthorTransform.position.Floor();

                Recalculate();
            }
        }

        private void OnDestroy()
        {
            Destroy(Mesh);
        }

        private void Recalculate()
        {
            RecalculateBoundingBox();
            TryCalculateLocalMeshData();
            ApplyMeshData();

            UpdatedMesh?.Invoke(this, Mesh);
            _ScheduledRecalculation = false;
        }

        private void TryCalculateLocalMeshData()
        {
            _Vertices.Clear();
            _Triangles.Clear();

            for (int x = -Radius; x < (Radius + 1); x++)
            {
                for (int y = -Radius; y < (Radius + 1); y++)
                {
                    for (int z = -Radius; z < (Radius + 1); z++)
                    {
                        Vector3 localPosition = new Vector3(x, y, z);
                        Vector3 globalPosition = _SelfTransform.position + localPosition;
                        
                        if (!WorldController.Current.TryGetBlockAt(globalPosition, out Block block)
                            || (block.Id == BlockController.BLOCK_EMPTY_ID)
                            || !block.HasAnyFaces())
                        {
                            continue;
                        }

                        if (block.HasFace(Direction.North))
                        {
                            AddTriangles(Direction.North);
                            AddVertices(Direction.North, localPosition);
                        }

                        if (block.HasFace(Direction.East))
                        {
                            AddTriangles(Direction.East);
                            AddVertices(Direction.East, localPosition);
                        }

                        if (block.HasFace(Direction.South))
                        {
                            AddTriangles(Direction.South);
                            AddVertices(Direction.South, localPosition);
                        }

                        if (block.HasFace(Direction.West))
                        {
                            AddTriangles(Direction.West);
                            AddVertices(Direction.West, localPosition);
                        }

                        if (block.HasFace(Direction.Up))
                        {
                            AddTriangles(Direction.Up);
                            AddVertices(Direction.Up, localPosition);
                        }

                        if (block.HasFace(Direction.Down))
                        {
                            AddTriangles(Direction.Down);
                            AddVertices(Direction.Down, localPosition);
                        }
                    }
                }
            }
        }

        private void AddTriangles(Direction direction)
        {
            foreach (int triangleValue in BlockFaces.Triangles.FaceTriangles[direction])
            {
                _Triangles.Add(_Vertices.Count + triangleValue);
            }
        }

        private void AddVertices(Direction direction, Vector3 localPosition)
        {
            foreach (Vector3 vertex in BlockFaces.Vertices.FaceVertices[direction])
            {
                _Vertices.Add(vertex + localPosition);
            }
        }

        private void ApplyMeshData()
        {
            if (Mesh == default)
            {
                Mesh = new Mesh();
            }
            else
            {
                Mesh.Clear();
            }

            Mesh.SetVertices(_Vertices);
            Mesh.SetTriangles(_Triangles, 0);

            Mesh.RecalculateNormals();
            Mesh.RecalculateTangents();
        }


        private void RecalculateBoundingBox()
        {
            // +1 to include center blocks / position
            int size = (Radius * 2) + 1;

            BoundingBox = new Bounds(_SelfTransform.position, new Vector3(size, size, size));
        }

        private void OnChunkMeshChangedInWorld(object sender, Bounds bounds)
        {
            _ScheduledRecalculation = true;
        }
    }
}
