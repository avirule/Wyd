#region

using System;
using System.Collections.Generic;
using System.Linq;
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

        private BlockFaces[] _BlockFaces;
        private Bounds _Bounds;
        private Vector3 _Position;
        private Vector3Int _Size;
        private int _YIndexStep;
        private List<ushort> _Blocks;

        public IEnumerable<ushort> EnumerableBlocks;
        public CancellationToken AbortToken;
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
                ResizeBlockFaces(_Size.Product());
            }
        }

        public ChunkMesher()
        {
            _BlockFaces = new BlockFaces[0];
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Triangles = new List<int>();
            _TransparentTriangles = new List<int>();
        }

        public void ClearInternalData()
        {
            for (int i = 0; i < _BlockFaces.Length; i++)
            {
                _BlockFaces[i].ClearFaces();
            }

            _Vertices.Clear();
            _Triangles.Clear();
            _TransparentTriangles.Clear();
            _UVs.Clear();
        }

        public void ResizeBlockFaces(int newSize)
        {
            if (_BlockFaces.Length == newSize)
            {
                return;
            }

            Array.Resize(ref _BlockFaces, newSize);
        }

        /// <summary>
        ///     Applies and returns processed <see cref="UnityEngine.Mesh" />.
        /// </summary>
        /// <param name="mesh">Given <see cref="UnityEngine.Mesh" /> to apply processed data to.</param>
        public void SetMesh(ref Mesh mesh)
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
        }

        public void GenerateMesh()
        {
            // enumerate blocks

            _Blocks = EnumerableBlocks.ToList();

            int index = -1;
            foreach (ushort blockId in _Blocks)
            {
                index += 1;

                if (blockId == BlockController.AIR_ID)
                {
                    continue;
                }

                Vector3Int localPosition = Mathv.GetIndexAsVector3Int(index, Size);

                if (BlockController.Current.CheckBlockHasProperty(blockId, BlockRule.Property.Transparent))
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

            if (!_BlockFaces[index].HasFace(Direction.North)
                && (((localPosition.z == (Size.z - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.forward, out ushort blockId)
                     && (blockId != _Blocks[index]))
                    || ((localPosition.z < (Size.z - 1))
                        && (_Blocks[index + Size.x] != _Blocks[index]))))
            {
                // set face of current block so it isn't traversed over again
                traversedFaces |= (byte)Direction.North;
                // add triangles for this block face
                AddTriangles(Direction.North, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.North, Direction.East,
                        1, Size.x, _Blocks[index]);

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
                            Direction.Up, _YIndexStep, Size.y, _Blocks[index]);

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
                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.North, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!_BlockFaces[index].HasFace(Direction.East)
                && (((localPosition.x == (Size.x - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out blockId)
                     && (_Blocks[index] != blockId))
                    || ((localPosition.x < (Size.x - 1))
                        && (_Blocks[index + 1] != _Blocks[index]))))
            {
                traversedFaces |= (byte)Direction.East;
                AddTriangles(Direction.East, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.East, Direction.North,
                        Size.x, Size.z, _Blocks[index]);

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
                            _YIndexStep, Size.y, _Blocks[index]);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.East, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (!_BlockFaces[index].HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out blockId)
                     && (_Blocks[index] != blockId))
                    || ((localPosition.z > 0)
                        && (_Blocks[index - Size.x] != _Blocks[index]))))
            {
                traversedFaces |= (byte)Direction.South;
                AddTriangles(Direction.South, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.South, Direction.East,
                        1, Size.x, _Blocks[index]);

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
                            _YIndexStep, Size.y, _Blocks[index]);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.South, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }


            if (!_BlockFaces[index].HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out blockId)
                     && (_Blocks[index] != blockId))
                    || ((localPosition.x > 0) && (_Blocks[index - 1] != _Blocks[index]))))
            {
                traversedFaces |= (byte)Direction.West;
                AddTriangles(Direction.West, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.West, Direction.North,
                        Size.x, Size.z, _Blocks[index]);

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
                            _YIndexStep, Size.y, _Blocks[index]);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.West, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!_BlockFaces[index].HasFace(Direction.Up)
                && ((localPosition.y == (Size.y - 1))
                    || ((localPosition.y < (Size.y - 1))
                        && (_Blocks[index + _YIndexStep] != _Blocks[index]))))
            {
                traversedFaces |= (byte)Direction.Up;
                AddTriangles(Direction.Up, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Up, Direction.North,
                        Size.x, Size.z, _Blocks[index]);

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
                            1, Size.x, _Blocks[index]);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.Up, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!_BlockFaces[index].HasFace(Direction.Down)
                && (localPosition.y > 0)
                && (_Blocks[index - _YIndexStep] != _Blocks[index]))
            {
                traversedFaces |= (byte)Direction.Down;
                AddTriangles(Direction.Down, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Down, Direction.North,
                        Size.x, Size.z, _Blocks[index]);

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
                            Direction.East, 1, Size.x, _Blocks[index]);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.Down, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            _BlockFaces[index].RawValue = traversedFaces;
        }

        private void TraverseIndex(int index, Vector3Int localPosition)
        {
            Vector3 globalPosition = _Position + localPosition;

            byte traversedFaces = 0;

            // ensure this block face hasn't already been traversed
            if (!_BlockFaces[index].HasFace(Direction.North)
                // check if we're on the far edge of the chunk, and if so, query WorldController for blocks in adjacent chunk
                && (((localPosition.z == (Size.z - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.forward, out ushort blockId)
                     && BlockController.Current.CheckBlockHasProperty(blockId, BlockRule.Property.Transparent))
                    // however if we're inside the chunk, use the proper Blocks[] array index for check 
                    || ((localPosition.z < (Size.z - 1))
                        && BlockController.Current.CheckBlockHasProperty(_Blocks[index + Size.x],
                            BlockRule.Property.Transparent))))
            {
                // set face of current block so it isn't traversed over
                traversedFaces |= (byte)Direction.North;
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
                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.North, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!_BlockFaces[index].HasFace(Direction.East)
                && (((localPosition.x == (Size.x - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out blockId)
                     && BlockController.Current.CheckBlockHasProperty(blockId, BlockRule.Property.Transparent))
                    || ((localPosition.x < (Size.x - 1))
                        && BlockController.Current.CheckBlockHasProperty(_Blocks[index + 1],
                            BlockRule.Property.Transparent))))
            {
                traversedFaces |= (byte)Direction.East;
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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.East, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (!_BlockFaces[index].HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out blockId)
                     && BlockController.Current.CheckBlockHasProperty(blockId, BlockRule.Property.Transparent))
                    || ((localPosition.z > 0)
                        && BlockController.Current.CheckBlockHasProperty(_Blocks[index - Size.x],
                            BlockRule.Property.Transparent))))
            {
                traversedFaces |= (byte)Direction.South;
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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.South, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            if (!_BlockFaces[index].HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out blockId)
                     && BlockController.Current.CheckBlockHasProperty(blockId, BlockRule.Property.Transparent))
                    || ((localPosition.x > 0)
                        && BlockController.Current.CheckBlockHasProperty(_Blocks[index - 1],
                            BlockRule.Property.Transparent))))
            {
                traversedFaces |= (byte)Direction.West;
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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.West, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                }
            }

            if (!_BlockFaces[index].HasFace(Direction.Up)
                && ((localPosition.y == (Size.y - 1))
                    || ((localPosition.y < (Size.y - 1))
                        && BlockController.Current.CheckBlockHasProperty(_Blocks[index + _YIndexStep],
                            BlockRule.Property.Transparent))))
            {
                traversedFaces |= (byte)Direction.Up;
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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.Up, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[3]);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!_BlockFaces[index].HasFace(Direction.Down)
                && (localPosition.y > 0)
                && BlockController.Current.CheckBlockHasProperty(_Blocks[index - _YIndexStep],
                    BlockRule.Property.Transparent))
            {
                traversedFaces |= (byte)Direction.Down;
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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                    Direction.Down, uvSize, out Vector3[] uvs))
                {
                    _UVs.Add(uvs[0]);
                    _UVs.Add(uvs[1]);
                    _UVs.Add(uvs[2]);
                    _UVs.Add(uvs[3]);
                }
            }

            _BlockFaces[index].RawValue = traversedFaces;
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
                && (_Blocks[index] == _Blocks[traversalIndex])
                && !_BlockFaces[traversalIndex].HasFace(faceDirection)
                // ensure the block to the north of our current block is transparent
                && WorldController.Current.TryGetBlockAt(
                    globalPosition + (traversals * traversalDirection.AsVector3()) + faceDirection.AsVector3(),
                    out ushort blockId)
                && (((id == -1)
                     && BlockController.Current.CheckBlockHasProperty(blockId, BlockRule.Property.Transparent))
                    || ((id > -1) && (id != blockId))))
            {
                _BlockFaces[traversalIndex].SetFace(faceDirection, true);

                // increment and set traversal values
                traversals++;
                traversalIndex = index + (traversals * traversalFactor);
            }

            return traversals;
        }

        #endregion
    }
}
