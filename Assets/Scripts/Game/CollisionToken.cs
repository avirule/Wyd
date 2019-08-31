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
        public Transform ParentEntityTransform;
        public Mesh Mesh;
        public int Radius;

        public event EventHandler<Mesh> UpdatedMesh;

        private void Awake()
        {
            ParentEntityTransform = transform.parent;
        }

        private void Update()
        {
            if (ParentEntityTransform == default)
            {
                return;
            }

            Vector3 difference = (transform.position - ParentEntityTransform.position).Abs();

            if (!Mathv.GreaterThanVector3(difference, Vector3.one))
            {
                return;
            }

            transform.position = ParentEntityTransform.position.Floor();
            (Vector3[] vertices, int[] triangles) = CalculateLocalMeshData();
            Mesh = ProvideNewMeshData(vertices, triangles);
            UpdatedMesh?.Invoke(this, Mesh);
        }

        public (Vector3[], int[]) CalculateLocalMeshData()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            for (int x = -Radius; x < (Radius + 1); x++)
            {
                for (int y = -Radius; y < (Radius + 1); y++)
                {
                    for (int z = -Radius; z < (Radius + 1); z++)
                    {
                        Vector3 globalPosition = transform.position + new Vector3(x, y, z);

                        if (WorldController.Current.GetBlockAtPosition(globalPosition) ==
                            BlockController._BLOCK_EMPTY_ID)
                        {
                            continue;
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAtPosition(globalPosition + Vector3.forward)))
                        {
                            triangles.AddRange(new[]
                            {
                                vertices.Count + 0,
                                vertices.Count + 2,
                                vertices.Count + 1,

                                vertices.Count + 2,
                                vertices.Count + 3,
                                vertices.Count + 1
                            });

                            vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z + 1),
                                new Vector3(x, y + 1, z + 1),
                                new Vector3(x + 1, y, z + 1),
                                new Vector3(x + 1, y + 1, z + 1)
                            });
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAtPosition(globalPosition + Vector3.right)))
                        {
                            triangles.AddRange(new[]
                            {
                                vertices.Count + 0,
                                vertices.Count + 2,
                                vertices.Count + 1,

                                vertices.Count + 2,
                                vertices.Count + 3,
                                vertices.Count + 1
                            });

                            vertices.AddRange(new[]
                            {
                                new Vector3(x + 1, y, z),
                                new Vector3(x + 1, y, z + 1),
                                new Vector3(x + 1, y + 1, z),
                                new Vector3(x + 1, y + 1, z + 1)
                            });
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAtPosition(globalPosition + Vector3.back)))
                        {
                            triangles.AddRange(new[]
                            {
                                vertices.Count + 0,
                                vertices.Count + 2,
                                vertices.Count + 1,

                                vertices.Count + 2,
                                vertices.Count + 3,
                                vertices.Count + 1
                            });

                            vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z),
                                new Vector3(x + 1, y, z),
                                new Vector3(x, y + 1, z),
                                new Vector3(x + 1, y + 1, z)
                            });
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAtPosition(globalPosition + Vector3.left)))
                        {
                            triangles.AddRange(new[]
                            {
                                vertices.Count + 0,
                                vertices.Count + 2,
                                vertices.Count + 1,

                                vertices.Count + 2,
                                vertices.Count + 3,
                                vertices.Count + 1
                            });

                            vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z),
                                new Vector3(x, y + 1, z),
                                new Vector3(x, y, z + 1),
                                new Vector3(x, y + 1, z + 1)
                            });
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAtPosition(globalPosition + Vector3.up)))
                        {
                            triangles.AddRange(new[]
                            {
                                vertices.Count + 0,
                                vertices.Count + 2,
                                vertices.Count + 1,

                                vertices.Count + 2,
                                vertices.Count + 3,
                                vertices.Count + 1
                            });

                            vertices.AddRange(new[]
                            {
                                new Vector3(x, y + 1, z),
                                new Vector3(x + 1, y + 1, z),
                                new Vector3(x, y + 1, z + 1),
                                new Vector3(x + 1, y + 1, z + 1)
                            });
                        }

                        if (BlockController.Current.IsBlockDefaultTransparent(
                            WorldController.Current.GetBlockAtPosition(globalPosition + Vector3.down)))
                        {
                            triangles.AddRange(new[]
                            {
                                vertices.Count + 0,
                                vertices.Count + 2,
                                vertices.Count + 1,

                                vertices.Count + 2,
                                vertices.Count + 3,
                                vertices.Count + 1
                            });

                            vertices.AddRange(new[]
                            {
                                new Vector3(x, y, z),
                                new Vector3(x, y, z + 1),
                                new Vector3(x + 1, y, z),
                                new Vector3(x + 1, y, z + 1)
                            });
                        }
                    }
                }
            }

            return (vertices.ToArray(), triangles.ToArray());
        }

        private static Mesh ProvideNewMeshData(Vector3[] vertices, int[] triangles)
        {
            Mesh mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();

            return mesh;
        }
    }
}