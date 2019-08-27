#region

using System.Collections.Generic;
using Controllers.Game;
using Controllers.World;
using Game.Terrain;
using Static;
using UnityEngine;

#endregion

namespace Game.Entity
{
    public class EntityTransformToken : MonoBehaviour
    {
        private MeshFilter _MeshFilter;
        private MeshCollider _MeshCollider;

        private Vector3Int _CurrentPosition;

        private Vector3Int CurrentPosition
        {
            get => _CurrentPosition;
            set
            {
                if (_CurrentPosition == value)
                {
                    return;
                }

                _CurrentPosition = value;
                transform.position = value;
            }
        }

        public Transform ParentEntityTransform;

        private void Awake()
        {
            _MeshFilter = GetComponent<MeshFilter>();
            _MeshCollider = GetComponent<MeshCollider>();
            ParentEntityTransform = transform.parent;
        }

        private void Update()
        {
            if (ParentEntityTransform == default)
            {
                return;
            }

            Vector3Int parentPositionInt32 = ParentEntityTransform.position.ToInt();

            if (parentPositionInt32 == CurrentPosition)
            {
            }

//            CurrentPosition = parentPositionInt32;
//            (Vector3[] vertices, int[] triangles) = CalculateLocalMeshData();
//            _MeshFilter.mesh = ProvideNewMeshData(vertices, triangles);
//            _MeshCollider.sharedMesh = _MeshFilter.mesh;
        }

        public (Vector3[], int[]) CalculateLocalMeshData()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            for (int x = -2; x < 3; x++)
            {
                for (int y = -2; y < 3; y++)
                {
                    for (int z = -2; z < 3; z++)
                    {
                        Vector3 globalPosition = ParentEntityTransform.position + new Vector3(x, y, z);

                        Block block = new Block();
                        WorldController.Current.GetBlockAtPosition(globalPosition.ToInt());

                        if (block.Id == BlockController.BLOCK_EMPTY_ID)
                        {
                            continue;
                        }

                        if (block.HasFace(Direction.North))
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

                        if (block.HasFace(Direction.East))
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

                        if (block.HasFace(Direction.South))
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

                        if (block.HasFace(Direction.West))
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

                        if (block.HasFace(Direction.Up))
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

                        if (block.HasFace(Direction.Down))
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

            mesh.RecalculateTangents();
            mesh.RecalculateNormals();
            mesh.Optimize();

            return mesh;
        }
    }
}