#region

using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.System;

// ReSharper disable TooWideLocalVariableScope

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMesher
    {
        private readonly List<Vector3> _Vertices;
        private readonly List<int> _Triangles;
        private readonly List<int> _TransparentTriangles;
        private readonly List<Vector3> _UVs;

        private Bounds _Bounds;
        private Vector3 _Position;
        private Vector3Int _Size;
        private int _YIndexStep;

        public CancellationToken AbortToken;
        public Block[] Blocks;
        public bool AggressiveFaceMerging;

        public Bounds Bounds
        {
            get => _Bounds;
            set
            {
                _Bounds = value;
                _Position = _Bounds.min;
            }
        }

        public Vector3Int Size
        {
            get => _Size;
            set
            {
                _Size = value;
                _YIndexStep = _Size.x * _Size.z;
            }
        }

        public ChunkMesher()
        {
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Triangles = new List<int>();
            _TransparentTriangles = new List<int>();
        }

        public void ClearInternalData()
        {
            _Vertices.Clear();
            _Triangles.Clear();
            _TransparentTriangles.Clear();
            _UVs.Clear();
        }

        /// <summary>
        ///     Applies and returns processed <see cref="UnityEngine.Mesh" />.
        /// </summary>
        /// <param name="mesh">Given <see cref="UnityEngine.Mesh" /> to apply processed data to.</param>
        /// <param name="immediateUpload"></param>
        /// <param name="readOnlyData"></param>
        public void SetMesh(ref Mesh mesh, bool immediateUpload = false, bool readOnlyData = false)
        {
            if ((_Vertices.Count == 0) || ((_Triangles.Count == 0) && (_TransparentTriangles.Count == 0)))
            {
                return;
            }

            mesh.Clear();

            mesh.subMeshCount = 2;
            mesh.indexFormat = _Vertices.Count > 65000
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            mesh.MarkDynamic();
            mesh.SetVertices(_Vertices);
            mesh.SetTriangles(_Triangles, 0);
            mesh.SetTriangles(_TransparentTriangles, 1);

            // check uvs count in case of no UVs to apply to mesh
            if (_UVs.Count > 0)
            {
                mesh.SetUVs(0, _UVs);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            if (immediateUpload)
            {
                mesh.UploadMeshData(readOnlyData);
            }
        }

        public void GenerateMesh()
        {
            for (int index = 0; (index < Blocks.Length) && !AbortToken.IsCancellationRequested; index++)
            {
                if (Blocks[index].Id == BlockController.Air.Id)
                {
                    continue;
                }

                Vector3Int localPosition = Mathv.GetIndexAsVector3Int(index, Size);

                if (Blocks[index].Transparent)
                {
                    TraverseIndexTransparent(index, localPosition);
                }
                else
                {
                    TraverseIndex(index, localPosition);
                }
            }
        }

        #region SIMPLER MESHING

        private void TraverseIndexTransparent(int index, Vector3Int localPosition)
        {
            Vector3 globalPosition = _Position + localPosition;

            byte traversedFaces = 0;
            
            if (!Blocks[index].HasFace(Direction.North)
                && (((localPosition.z == (Size.z - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.forward, out Block block)
                     && (block.Id != Blocks[index].Id))
                    || ((localPosition.z < (Size.z - 1))
                        && (Blocks[index + Size.x].Id != Blocks[index].Id))))
            {
                // todo fix northern transparent faces sometimes not culling inner faces

                // set face of current block so it isn't traversed over
                traversedFaces |= (byte) Direction.North;
                // add triangles for this block face
                AddTriangles(Direction.North, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.North, Direction.East,
                        1, Size.x, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.North,
                            Direction.Up, _YIndexStep, Size.y, Blocks[index].Id);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    // no traversals found, so just add vertices for a regular 1x1 face
                    AddVertices(Direction.North, localPosition);
                }

                // attempt to retrieve and add uvs for block face
                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.North, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.East)
                && (((localPosition.x == (Size.x - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out block)
                     && (Blocks[index].Id != block.Id))
                    || ((localPosition.x < (Size.x - 1))
                        && (Blocks[index + 1].Id != Blocks[index].Id))))
            {
                traversedFaces |= (byte) Direction.East;
                AddTriangles(Direction.East, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.East, Direction.North,
                        Size.x, Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.East, Direction.Up,
                            _YIndexStep, Size.y, Blocks[index].Id);

                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.East, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.East, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (!Blocks[index].HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out block)
                     && (Blocks[index].Id != block.Id))
                    || ((localPosition.z > 0)
                        && (Blocks[index - Size.x].Id != Blocks[index].Id))))
            {
                traversedFaces |= (byte) Direction.South;
                AddTriangles(Direction.South, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.South, Direction.East,
                        1, Size.x, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.South,
                            Direction.Up,
                            _YIndexStep, Size.y, Blocks[index].Id);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.South, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.South, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }


            if (!Blocks[index].HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out block)
                     && (Blocks[index].Id != block.Id))
                    || ((localPosition.x > 0) && (Blocks[index - 1].Id != Blocks[index].Id))))
            {
                traversedFaces |= (byte) Direction.West;
                AddTriangles(Direction.West, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.West, Direction.North,
                        Size.x, Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.West, Direction.Up,
                            _YIndexStep, Size.y, Blocks[index].Id);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.West, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.West, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.Up)
                && (((localPosition.y == (Size.y - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.up, out block)
                     && (block.Id != Blocks[index].Id))
                    || ((localPosition.y < (Size.y - 1))
                        && (Blocks[index + _YIndexStep].Id != Blocks[index].Id))))
            {
                traversedFaces |= (byte) Direction.Up;
                AddTriangles(Direction.Up, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Up, Direction.North,
                        Size.x, Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Up, Direction.East,
                            1, Size.x, Blocks[index].Id);

                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Up, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Up, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!Blocks[index].HasFace(Direction.Down)
                && (localPosition.y > 0)
                && (Blocks[index - _YIndexStep].Id != Blocks[index].Id))
            {
                traversedFaces |= (byte) Direction.Down;
                AddTriangles(Direction.Down, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Down, Direction.North,
                        Size.x, Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Down,
                            Direction.East, 1, Size.x, Blocks[index].Id);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Down, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Down, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            Blocks[index].Faces = traversedFaces;
        }

        private void TraverseIndex(int index, Vector3Int localPosition)
        {
            Vector3 globalPosition = _Position + localPosition;

            byte traversedFaces = 0;
            
            // ensure this block face hasn't already been traversed
            if (!Blocks[index].HasFace(Direction.North)
                // check if we're on the far edge of the chunk, and if so, query WorldController for blocks in adjacent chunk
                && (((localPosition.z == (Size.z - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.forward, out Block block)
                     && block.Transparent)
                    // however if we're inside the chunk, use the proper Blocks[] array index for check 
                    || ((localPosition.z < (Size.z - 1))
                        && Blocks[index + Size.x].Transparent)))
            {
                // set face of current block so it isn't traversed over
                traversedFaces |= (byte) Direction.North;
                // add triangles for this block face
                AddTriangles(Direction.North);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.North, Direction.East,
                        1,
                        Size.x);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.North,
                            Direction.Up, _YIndexStep, Size.y);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    // no traversals found, so just add vertices for a regular 1x1 face
                    AddVertices(Direction.North, localPosition);
                }

                // attempt to retrieve and add uvs for block face
                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.North, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.East)
                && (((localPosition.x == (Size.x - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out block)
                     && block.Transparent)
                    || ((localPosition.x < (Size.x - 1)) && Blocks[index + 1].Transparent)))
            {
                traversedFaces |= (byte) Direction.East;
                AddTriangles(Direction.East);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.East, Direction.North,
                        Size.x, Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.East, Direction.Up,
                            _YIndexStep, Size.y);

                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.East, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.East, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (!Blocks[index].HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out block)
                     && block.Transparent)
                    || ((localPosition.z > 0) && Blocks[index - Size.x].Transparent)))
            {
                traversedFaces |= (byte) Direction.South;
                AddTriangles(Direction.South);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.South, Direction.East,
                        1, Size.x);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.South,
                            Direction.Up, _YIndexStep, Size.y);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.South, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.South, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (!Blocks[index].HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out block)
                     && block.Transparent)
                    || ((localPosition.x > 0) && Blocks[index - 1].Transparent)))
            {
                traversedFaces |= (byte) Direction.West;
                AddTriangles(Direction.West);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.West, Direction.North,
                        Size.x, Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.West, Direction.Up,
                            _YIndexStep, Size.y);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.West, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.West, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.Up)
                && (((localPosition.y == (Size.y - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.up, out block)
                     && block.Transparent)
                    || ((localPosition.y < (Size.y - 1))
                        && Blocks[index + _YIndexStep].Transparent)))
            {
                traversedFaces |= (byte) Direction.Up;
                AddTriangles(Direction.Up);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Up, Direction.North,
                        Size.x, Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Up, Direction.East,
                            1, Size.x);

                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Up, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Up, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!Blocks[index].HasFace(Direction.Down)
                && (localPosition.y > 0)
                && Blocks[index - _YIndexStep].Transparent)
            {
                traversedFaces |= (byte) Direction.Down;
                AddTriangles(Direction.Down);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Down, Direction.North,
                        Size.x, Size.z);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Down,
                            Direction.East, 1, Size.x);

                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Down, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(Blocks[index].Id, globalPosition,
                    Direction.Down, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            Blocks[index].Faces = traversedFaces;
        }

        private void AddTriangles(Direction direction, bool transparent = false)
        {
            foreach (int triangleValue in BlockFaces.Triangles.FaceTriangles[direction])
            {
                if (transparent)
                {
                    _TransparentTriangles.Add(_Vertices.Count + triangleValue);
                }
                else
                {
                    _Triangles.Add(_Vertices.Count + triangleValue);
                }
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

        /// <summary>
        ///     Gets the total amount of possible traversals for face merging in a direction
        /// </summary>
        /// <param name="index">1D index of current block.</param>
        /// <param name="globalPosition">Global position of starting block.</param>
        /// <param name="slice">Current slice (x, y, or z) of a 3D index relative to your traversal direction.</param>
        /// <param name="traversalDirection">Direction to traverse in.</param>
        /// <param name="faceDirection">Direction to check faces while traversing.</param>
        /// <param name="traversalFactor">Amount of indexes to move forwards for each successful traversal in given direction.</param>
        /// <param name="limitingSliceValue">Maximum amount of traversals in given traversal direction.</param>
        /// <param name="id">Block ID of current block.</param>
        /// <returns><see cref="int" /> representing how many successful traversals were made in the given direction.</returns>
        private int GetTraversals(
            int index, Vector3 globalPosition, int slice, Direction faceDirection,
            Direction traversalDirection, int traversalFactor, int limitingSliceValue, int id = -1)
        {
            // 1 being the current block at `index`
            int traversals = 1;

            // todo make aggressive face merging compatible with special block shapes
            if (!AggressiveFaceMerging)
            {
                return traversals;
            }

            // incrementing on x, so the traversal factor is 1
            // if we were incrementing on z, the factor would be Chunk.Size.x
            // and on y it would be (Chunk.YIndexStep)
            int traversalIndex = index + (traversals * traversalFactor);

            while ( // Set traversalIndex and ensure it is within the chunk's context
                ((slice + traversals) < limitingSliceValue)
                // This check removes the need to check if the adjacent block is transparent,
                // as our current block will never be transparent
                && (Blocks[index].Id == Blocks[traversalIndex].Id)
                && !Blocks[traversalIndex].HasFace(faceDirection)
                // ensure the block to the north of our current block is transparent
                && WorldController.Current.TryGetBlockAt(
                    globalPosition + (traversals * traversalDirection.AsVector3()) + faceDirection.AsVector3(),
                    out Block block)
                && (((id == -1) && block.Transparent) || ((id > -1) && (id != block.Id))))
            {
                Blocks[traversalIndex].SetFace(faceDirection, true);

                // increment and set traversal values
                traversals++;
                traversalIndex = index + (traversals * traversalFactor);
            }

            return traversals;
        }

        #endregion
    }
}
