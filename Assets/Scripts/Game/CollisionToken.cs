#region

using System;
using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using UnityEngine;

#endregion

namespace Game
{
    public class CollisionToken : MonoBehaviour
    {
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
            _Vertices = new List<Vector3>();
            _Triangles = new List<int>();

            AuthorTransform = transform.parent;
        }

        private void Update()
        {
            if (AuthorTransform == default)
            {
                return;
            }

            Vector3 difference = (transform.position - AuthorTransform.position).Abs();

            if (!Mathv.GreaterThanVector3(difference, Vector3.one) && !_ScheduledRecalculation)
            {
                return;
            }

            transform.position = AuthorTransform.position.Floor();

            Recalculate();
        }

        private void OnDestroy()
        {
            Destroy(Mesh);
        }

        private void Recalculate()
        {
            RecalculateBoundingBox();
            CalculateLocalMeshData();
            ApplyMeshData();

            UpdatedMesh?.Invoke(this, Mesh);
            _ScheduledRecalculation = false;
        }

        private void CalculateLocalMeshData()
        {
            _Vertices.Clear();
            _Triangles.Clear();

            for (int x = -Radius; x < (Radius + 1); x++)
            {
                for (int y = -Radius; y < (Radius + 1); y++)
                {
                    for (int z = -Radius; z < (Radius + 1); z++)
                    {
                        Vector3 globalPosition = transform.position + new Vector3(x, y, z);

                        if (WorldController.Current.GetBlockAt(globalPosition) ==
                            BlockController.BLOCK_EMPTY_ID)
                        {
                            continue;
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAt(globalPosition + Vector3.forward)))
                        {
                            _Triangles.Add(_Vertices.Count + 0);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 1);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 3);
                            _Triangles.Add(_Vertices.Count + 1);

                            _Vertices.Add(new Vector3(x, y, z + 1));
                            _Vertices.Add(new Vector3(x, y + 1, z + 1));
                            _Vertices.Add(new Vector3(x + 1, y, z + 1));
                            _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAt(globalPosition + Vector3.right)))
                        {
                            _Triangles.Add(_Vertices.Count + 0);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 1);

                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 3);
                            _Triangles.Add(_Vertices.Count + 1);

                            _Vertices.Add(new Vector3(x + 1, y, z));
                            _Vertices.Add(new Vector3(x + 1, y, z + 1));
                            _Vertices.Add(new Vector3(x + 1, y + 1, z));
                            _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAt(globalPosition + Vector3.back)))
                        {
                            _Triangles.Add(_Vertices.Count + 0);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 1);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 3);
                            _Triangles.Add(_Vertices.Count + 1);

                            _Vertices.Add(new Vector3(x, y, z));
                            _Vertices.Add(new Vector3(x + 1, y, z));
                            _Vertices.Add(new Vector3(x, y + 1, z));
                            _Vertices.Add(new Vector3(x + 1, y + 1, z));
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAt(globalPosition + Vector3.left)))
                        {
                            _Triangles.Add(_Vertices.Count + 0);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 1);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 3);
                            _Triangles.Add(_Vertices.Count + 1);

                            _Vertices.Add(new Vector3(x, y, z));
                            _Vertices.Add(new Vector3(x, y + 1, z));
                            _Vertices.Add(new Vector3(x, y, z + 1));
                            _Vertices.Add(new Vector3(x, y + 1, z + 1));
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAt(globalPosition + Vector3.up)))
                        {
                            _Triangles.Add(_Vertices.Count + 0);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 1);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 3);
                            _Triangles.Add(_Vertices.Count + 1);

                            _Vertices.Add(new Vector3(x, y + 1, z));
                            _Vertices.Add(new Vector3(x + 1, y + 1, z));
                            _Vertices.Add(new Vector3(x, y + 1, z + 1));
                            _Vertices.Add(new Vector3(x + 1, y + 1, z + 1));
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAt(globalPosition + Vector3.down)))
                        {
                            _Triangles.Add(_Vertices.Count + 0);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 1);
                            _Triangles.Add(_Vertices.Count + 2);
                            _Triangles.Add(_Vertices.Count + 3);
                            _Triangles.Add(_Vertices.Count + 1);

                            _Vertices.Add(new Vector3(x, y, z));
                            _Vertices.Add(new Vector3(x, y, z + 1));
                            _Vertices.Add(new Vector3(x + 1, y, z));
                            _Vertices.Add(new Vector3(x + 1, y, z + 1));
                        }
                    }
                }
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

            BoundingBox = new Bounds(transform.position, new Vector3(size, size, size));
        }
    }
}