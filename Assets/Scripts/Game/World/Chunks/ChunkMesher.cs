#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.Game.World.Blocks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Graphics;

// ReSharper disable TooWideLocalVariableScope

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMesher
    {
        private readonly Stopwatch _Stopwatch;
        private readonly HashSet<ushort> _TransparentIDCache;
        private readonly HashSet<ushort> _OpaqueIDCache;
        private readonly MeshBlock[] _Mask;
        private readonly MeshData _MeshData;

        private CancellationToken _CancellationToken;
        private float3 _OriginPoint;
        private OctreeNode<ushort> _Blocks;
        private bool _AggressiveFaceMerging;

        public TimeSpan SetBlockTimeSpan { get; private set; }
        public TimeSpan MeshingTimeSpan { get; private set; }

        public ChunkMesher(CancellationToken cancellationToken, float3 originPoint, OctreeNode<ushort> blocks,
            bool aggressiveFaceMerging)
        {
            if (blocks == null)
            {
                return;
            }

            _Stopwatch = new Stopwatch();
            _TransparentIDCache = new HashSet<ushort>();
            _OpaqueIDCache = new HashSet<ushort>();
            _MeshData = new MeshData(
                new List<Vector3>(ChunkController.SIZE_CUBED),
                new List<Vector3>(ChunkController.SIZE_CUBED),
                new List<int>(ChunkController.SIZE_CUBED), // triangles
                new List<int>(ChunkController.SIZE_CUBED)); // transparent triangles
            _Mask =  new MeshBlock[ChunkController.SIZE_CUBED];

            PrepareMeshing(cancellationToken, originPoint, blocks, aggressiveFaceMerging);
        }

        public void PrepareMeshing(CancellationToken cancellationToken, float3 originPoint, OctreeNode<ushort> blocks,
            bool aggressiveFaceMerging)
        {
            _CancellationToken = cancellationToken;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveFaceMerging;

            _MeshData.Clear();
        }

        public void Reset()
        {
            _MeshData.Clear();
            _Blocks = null;
        }

        public void ApplyMeshData(ref Mesh mesh) => _MeshData.ApplyMeshData(ref mesh);

        public void GenerateMesh()
        {
            if ((_Blocks == null) || (_Blocks.IsUniform && (_Blocks.Value == BlockController.AirID)))
            {
                return;
            }

            _Stopwatch.Restart();
            SetBlockData(_Blocks.GetAllData());
            _Stopwatch.Stop();
            SetBlockTimeSpan = _Stopwatch.Elapsed;

            _Stopwatch.Restart();
            int index = -1;
            foreach (MeshBlock block in _Mask)
            {
                if (_CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                index += 1;

                if (block.Id == BlockController.AirID)
                {
                    continue;
                }

                int3 localPosition = WydMath.IndexTo3D(index, ChunkController.Size3D);

                if (CheckBlockTransparency(block.Id))
                {
                    TraverseIndexTransparent(WydMath.ToInt(_OriginPoint), index, localPosition);
                }
                else
                {
                    TraverseIndex(WydMath.ToInt(_OriginPoint), index, localPosition);
                }
            }

            _Stopwatch.Stop();
            MeshingTimeSpan = _Stopwatch.Elapsed;
        }

        private void SetBlockData(IEnumerable<ushort> blocks)
        {
            int index = 0;

            foreach (ushort id in blocks)
            {
                if (_CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _Mask[index].Id = id;
                _Mask[index].Faces.ClearFaces();

                index += 1;
            }
        }

        private bool CheckBlockTransparency(ushort blockId)
        {
            if (_TransparentIDCache.Contains(blockId))
            {
                return true;
            }
            else if (_OpaqueIDCache.Contains(blockId))
            {
                return false;
            }
            else if (BlockController.Current.CheckBlockHasProperty(blockId, BlockDefinition.Property.Transparent))
            {
                _TransparentIDCache.Add(blockId);
                return true;
            }
            else
            {
                _OpaqueIDCache.Add(blockId);
                return false;
            }
        }

        #region TRAVERSAL MESHING

        private void TraverseIndexTransparent(int3 position, int index, int3 localPosition)
        {
            int3 globalPosition = position + localPosition;

            if (!_Mask[index].Faces.HasFace(Direction.North)
                && (((localPosition.z == (ChunkController.Size3D.z - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.North, out ushort blockId)
                     && (blockId != _Mask[index].Id))
                    || ((localPosition.z < (ChunkController.Size3D.z - 1))
                        && (_Mask[index + ChunkController.Size3D.x].Id != _Mask[index].Id))))
            {
                // set face of current block so it isn't traversed over again
                _Mask[index].Faces.SetFace(Direction.North, true);
                // add triangles for this block face
                AddTriangles(Direction.North, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.North,
                        Direction.East, 1, true);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.North,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, true);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 1f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    // no traversals found, so just add vertices for a regular 1x1 face
                    AddVertices(Direction.North, localPosition);
                }

                // attempt to retrieve and add uvs for block face
                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.North, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.TopRight);
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.East)
                && (((localPosition.x == (ChunkController.Size3D.x - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.East, out blockId)
                     && (_Mask[index].Id != blockId))
                    || ((localPosition.x < (ChunkController.Size3D.x - 1))
                        && (_Mask[index + 1].Id != _Mask[index].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.East, true);
                AddTriangles(Direction.East, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.East,
                        Direction.North, ChunkController.Size3D.x, true);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.East,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, true);

                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.East, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.East, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.South, out blockId)
                     && (_Mask[index].Id != blockId))
                    || ((localPosition.z > 0)
                        && (_Mask[index - ChunkController.Size3D.x].Id != _Mask[index].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.South, true);
                AddTriangles(Direction.South, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.South,
                        Direction.East, 1, true);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.South,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, true);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 0f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.South, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.South, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }


            if (!_Mask[index].Faces.HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.West, out blockId)
                     && (_Mask[index].Id != blockId))
                    || ((localPosition.x > 0) && (_Mask[index - 1].Id != _Mask[index].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.West, true);
                AddTriangles(Direction.West, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.West,
                        Direction.North, ChunkController.Size3D.x, true);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.West,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, true);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.West, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.West, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.TopRight);
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.Up)
                && ((localPosition.y == (ChunkController.Size3D.y - 1))
                    || ((localPosition.y < (ChunkController.Size3D.y - 1))
                        && (_Mask[index + ChunkController.SIZE_VERTICAL_STEP].Id != _Mask[index].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.Up, true);
                AddTriangles(Direction.Up, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.Up,
                        Direction.North, ChunkController.Size3D.x, true);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.Up,
                            Direction.East, 1, true);

                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Up, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.Up, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!_Mask[index].Faces.HasFace(Direction.Down)
                && (localPosition.y > 0)
                && (_Mask[index - ChunkController.SIZE_VERTICAL_STEP].Id != _Mask[index].Id))
            {
                _Mask[index].Faces.SetFace(Direction.Down, true);
                AddTriangles(Direction.Down, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.Down,
                        Direction.North,
                        ChunkController.Size3D.x, true);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.Down,
                            Direction.East, 1, true);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Down, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.Down, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }
        }

        private void TraverseIndex(int3 origin, int index, int3 localPosition)
        {
            int3 globalPosition = origin + localPosition;

            // ensure this block face hasn't already been traversed
            if (!_Mask[index].Faces.HasFace(Direction.North)
                // check if we're on the far edge of the chunk, and if so, query WorldController for blocks in adjacent chunk
                && (((localPosition.z == (ChunkController.Size3D.z - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.North, out ushort blockId)
                     && CheckBlockTransparency(blockId))
                    // however if we're inside the chunk, use the _Mask[] array index for check
                    || ((localPosition.z < (ChunkController.Size3D.z - 1))
                        && CheckBlockTransparency(_Mask[index + ChunkController.Size3D.x].Id))))
            {
                // set face of current block so it isn't traversed over
                _Mask[index].Faces.SetFace(Direction.North, true);
                // add triangles for this block face
                AddTriangles(Direction.North);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.North,
                        Direction.East, 1, false);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.North,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, false);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 1f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    // no traversals found, so just add vertices for a regular 1x1 face
                    AddVertices(Direction.North, localPosition);
                }

                // attempt to retrieve and add uvs for block face
                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.North, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.TopRight);
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.East)
                && (((localPosition.x == (ChunkController.Size3D.x - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.East, out blockId)
                     && CheckBlockTransparency(blockId))
                    || ((localPosition.x < (ChunkController.Size3D.x - 1))
                        && CheckBlockTransparency(_Mask[index + 1].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.East, true);
                AddTriangles(Direction.East);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.East,
                        Direction.North, ChunkController.Size3D.x, false);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.East,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, false);

                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.East, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.East, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.South, out blockId)
                     && CheckBlockTransparency(blockId))
                    || ((localPosition.z > 0)
                        && CheckBlockTransparency(_Mask[index - ChunkController.Size3D.x].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.South, true);
                AddTriangles(Direction.South);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.South,
                        Direction.East, 1, false);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.South,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, false);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, traversals, 0f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.South, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.South, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.West, out blockId)
                     && CheckBlockTransparency(blockId))
                    || ((localPosition.x > 0)
                        && CheckBlockTransparency(_Mask[index - 1].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.West, true);
                AddTriangles(Direction.West);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.West,
                        Direction.North, ChunkController.Size3D.x, false);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, ChunkController.Size3D.y, Direction.West,
                            Direction.Up, ChunkController.SIZE_VERTICAL_STEP, false);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(0f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.West, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.West, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.TopRight);
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.Up)
                && (((localPosition.y == (ChunkController.Size3D.y - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.Up, out blockId)
                     && CheckBlockTransparency(blockId))
                    || ((localPosition.y < (ChunkController.Size3D.y - 1))
                        && CheckBlockTransparency(_Mask[index + ChunkController.SIZE_VERTICAL_STEP].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.Up, true);
                AddTriangles(Direction.Up);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.Up,
                        Direction.North, ChunkController.Size3D.x, false);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.Up,
                            Direction.East, 1, false);

                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 1f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 1f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Up, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.Up, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!_Mask[index].Faces.HasFace(Direction.Down)
                && (((localPosition.y == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.Down, out blockId)
                     && CheckBlockTransparency(blockId))
                    || ((localPosition.y > 0)
                        && CheckBlockTransparency(_Mask[index - ChunkController.SIZE_VERTICAL_STEP].Id))))
            {
                _Mask[index].Faces.SetFace(Direction.Down, true);
                AddTriangles(Direction.Down);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, ChunkController.Size3D.z, Direction.Down,
                        Direction.North, ChunkController.Size3D.x, false);

                    if (traversals > 1)
                    {
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, traversals));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, ChunkController.Size3D.x, Direction.Down,
                            Direction.East, 1, false);

                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(0f, 0f, 1f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 0f));
                        _MeshData.AddVertex(localPosition + new float3(traversals, 0f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Down, localPosition);
                }

                if (BlockController.Current.GetUVs(_Mask[index].Id, globalPosition,
                    Direction.Down, uvSize, out BlockUVs blockUVs))
                {
                    _MeshData.AddUV(blockUVs.BottomLeft);
                    _MeshData.AddUV(blockUVs.TopLeft);
                    _MeshData.AddUV(blockUVs.BottomRight);
                    _MeshData.AddUV(blockUVs.TopRight);
                }
            }
        }

        private void AddTriangles(Direction direction, bool transparent = false)
        {
                if (transparent)
                {
                    _MeshData.AddTriangles(1, BlockFaces.Triangles.FaceTriangles[direction]
                        .Select(triangle => triangle + _MeshData.Vertices.Count));
                }
                else
                {
                    _MeshData.AddTriangles(0, BlockFaces.Triangles.FaceTriangles[direction]
                        .Select(triangle => triangle + _MeshData.Vertices.Count));
                }
        }

        private void AddVertices(Direction direction, int3 localPosition)
        {
            foreach (float3 vertex in BlockFaces.Vertices.FaceVertices[direction])
            {
                _MeshData.AddVertex(vertex + localPosition);
            }
        }

        /// <summary>
        ///     Gets the total amount of possible traversals for face merging in a direction
        /// </summary>
        /// <param name="index">1D index of current block.</param>
        /// <param name="globalPosition">Global position of starting block.</param>
        /// <param name="slice">Current slice (x, y, or z) of a 3D index relative to your traversal direction.</param>
        /// <param name="limitingSliceValue">Maximum amount of traversals in given traversal direction.</param>
        /// <param name="traversalDirection">Direction to traverse in.</param>
        /// <param name="faceDirection">Direction to check faces while traversing.</param>
        /// <param name="traversalFactor">Amount of indexes to move forwards for each successful traversal in given direction.</param>
        /// <param name="transparentTraversal">Determines whether or not transparent traversal will be used.</param>
        /// <returns><see cref="int" /> representing how many successful traversals were made in the given traversal direction.</returns>
        private int GetTraversals(int index, int3 globalPosition, int slice, int limitingSliceValue,
            Direction faceDirection, Direction traversalDirection, int traversalFactor, bool transparentTraversal)
        {
            if (!_AggressiveFaceMerging)
            {
                return 1;
            }

            int traversals = 1;
            int traversalIndex;

            int3 traversalNormal = traversalDirection.ToInt3();
            int3 faceNormal = faceDirection.ToInt3();

            while ((slice + traversals) < limitingSliceValue)
            {
                // incrementing on x, so the traversal factor is 1
                // if we were incrementing on z, the factor would be ChunkController.Size3D.x
                // and on y it would be (ChunkController.Size3D.x * ChunkController.Size3D.z)
                traversalIndex = index + (traversals * traversalFactor);

                if ((_Mask[index].Id != _Mask[traversalIndex].Id)
                    || _Mask[traversalIndex].Faces.HasFace(faceDirection)
                    // ensure the block adjacent to our current block is transparent
                    || !WorldController.Current.TryGetBlock(
                        globalPosition + (traversals * traversalNormal) + faceNormal, out ushort blockId)
                    // if transparent, traverse as long as block is the same
                    // if opaque, traverse as long as normal-adjacent block is transparent
                    || (transparentTraversal && (_Mask[index].Id != blockId))
                    || !CheckBlockTransparency(blockId))
                {
                    break;
                }

                // set face to traversed and continue traversal
                _Mask[traversalIndex].Faces.SetFace(faceDirection, true);

                // increment and set traversal value
                traversals += 1;
            }


            return traversals;
        }

        #endregion
    }
}
