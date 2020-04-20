#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.Graphics;
using Wyd.System;
using Wyd.System.Collections;

// ReSharper disable TooWideLocalVariableScope

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMesher
    {
        private static readonly ObjectCache<MeshBlock[]> _masksCache = new ObjectCache<MeshBlock[]>();

        private readonly Stopwatch _Stopwatch;
        private readonly List<Vector3> _Vertices;
        private readonly List<int> _Triangles;
        private readonly List<int> _TransparentTriangles;
        private readonly List<Vector3> _UVs;

        private readonly CancellationToken _CancellationToken;
        private readonly float3 _OriginPoint;
        private readonly OctreeNode _Blocks;
        private readonly bool _AggressiveFaceMerging;
        private readonly int3 _Size;
        private readonly int _VerticalIndexStep;

        private MeshBlock[] _Mask;

        public TimeSpan SetBlockTimeSpan { get; private set; }
        public TimeSpan MeshingTimeSpan { get; private set; }
        public ChunkMeshData MeshData { get; private set; }

        public ChunkMesher(CancellationToken cancellationToken, float3 originPoint, OctreeNode blocks,
            bool aggressiveFaceMerging)
        {
            if (blocks == null)
            {
                return;
            }

            _Stopwatch = new Stopwatch();
            _Mask = new MeshBlock[0];
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Triangles = new List<int>();
            _TransparentTriangles = new List<int>();

            _CancellationToken = cancellationToken;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _Size = WydMath.ToInt(_Blocks.Volume.Size);
            _VerticalIndexStep = _Size.x * _Size.z;

            int sizeProduct = WydMath.Product(_Size);
            if (_Mask.Length != sizeProduct)
            {
                _Mask = new MeshBlock[sizeProduct];
            }

            _AggressiveFaceMerging = aggressiveFaceMerging;

            _Mask = _masksCache.Retrieve() ?? new MeshBlock[sizeProduct];
        }

        public void GenerateMesh()
        {
            if ((_Blocks == null) || (_Blocks.IsUniform && (_Blocks.Value == BlockController.AirID)))
            {
                return;
            }

            _Stopwatch.Restart();
            SetBlockData(_Blocks.UncheckedGetAllData());
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

                int3 localPosition = WydMath.IndexTo3D(index, _Size);

                if (BlockController.Current.CheckBlockHasProperties(block.Id, BlockDefinition.Property.Transparent))
                {
                    //TraverseIndexTransparent(index, localPosition);
                }
                else
                {
                    TraverseIndex(WydMath.ToInt(_OriginPoint), index, localPosition);
                }
            }

            _masksCache.CacheItem(ref _Mask);

            MeshData = new ChunkMeshData(_Vertices, _Triangles, _TransparentTriangles, _UVs);

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

        #region TRAVERSAL MESHING

        // private void TraverseIndexTransparent(int3 position, int index, int3 localPosition)
        // {
        //     int3 globalPosition = position + localPosition;
        //
        //     if (!_Blocks[index].Faces.HasFace(Direction.North)
        //         && (((localPosition.z == (_Size.z - 1))
        //              && WorldController.Current.TryGetBlockAt(globalPosition + Directions.North, out ushort blockId)
        //              && (blockId != _Blocks[index].Id))
        //             || ((localPosition.z < (_Size.z - 1))
        //                 && (_Blocks[index + _Size.x].Id != _Blocks[index].Id))))
        //     {
        //         // set face of current block so it isn't traversed over again
        //         _Blocks[index].Faces.SetFace(Direction.North, true);
        //         // add triangles for this block face
        //         AddTriangles(Direction.North, true);
        //
        //         int traversals;
        //         Vector3 uvSize = Vector3.one;
        //
        //         if (_AggressiveFaceMerging)
        //         {
        //             traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.North, Direction.East,
        //                 1, _Size.x, _Blocks[index].Id);
        //
        //             if (traversals > 1)
        //             {
        //                 // The traversals value goes into the vertex points that have a positive value
        //                 // on the same axis as your slice value.
        //                 // So for instance, we were traversing on the x, so we'll be extending the x point of our
        //                 // vertices by the number of successful traversals.
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
        //                 uvSize.x = traversals;
        //             }
        //             else
        //             {
        //                 // if traversal failed (no blocks found in probed direction) then look on next axis
        //                 traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.North,
        //                     Direction.Up, _VerticalIndexStep, _Size.y, _Blocks[index].Id);
        //
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
        //                 uvSize.z = traversals;
        //             }
        //         }
        //         else
        //         {
        //             // no traversals found, so just add vertices for a regular 1x1 face
        //             AddVertices(Direction.North, localPosition);
        //         }
        //
        //         // attempt to retrieve and add uvs for block face
        //         if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
        //             Direction.North, uvSize, out BlockUVs blockUVs))
        //         {
        //             _UVs.Add(blockUVs.TopLeft);
        //             _UVs.Add(blockUVs.TopRight);
        //             _UVs.Add(blockUVs.BottomLeft);
        //             _UVs.Add(blockUVs.BottomRight);
        //         }
        //     }
        //
        //     if (!_Blocks[index].Faces.HasFace(Direction.East)
        //         && (((localPosition.x == (_Size.x - 1))
        //              && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out blockId)
        //              && (_Blocks[index].Id != blockId))
        //             || ((localPosition.x < (_Size.x - 1))
        //                 && (_Blocks[index + 1].Id != _Blocks[index].Id))))
        //     {
        //         _Blocks[index].Faces.SetFace(Direction.East, true);
        //         AddTriangles(Direction.East, true);
        //
        //         int traversals;
        //         Vector3 uvSize = Vector3.one;
        //
        //         if (_AggressiveFaceMerging)
        //         {
        //             traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.East, Direction.North,
        //                 _Size.x, _Size.z, _Blocks[index].Id);
        //
        //             if (traversals > 1)
        //             {
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
        //                 uvSize.x = traversals;
        //             }
        //             else
        //             {
        //                 traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.East, Direction.Up,
        //                     _VerticalIndexStep, _Size.y, _Blocks[index].Id);
        //
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
        //                 uvSize.z = traversals;
        //             }
        //         }
        //         else
        //         {
        //             AddVertices(Direction.East, localPosition);
        //         }
        //
        //         if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
        //             Direction.East, uvSize, out BlockUVs blockUVs))
        //         {
        //             _UVs.Add(blockUVs.BottomLeft);
        //             _UVs.Add(blockUVs.TopLeft);
        //             _UVs.Add(blockUVs.BottomRight);
        //             _UVs.Add(blockUVs.TopRight);
        //         }
        //     }
        //
        //     if (!_Blocks[index].Faces.HasFace(Direction.South)
        //         && (((localPosition.z == 0)
        //              && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out blockId)
        //              && (_Blocks[index].Id != blockId))
        //             || ((localPosition.z > 0)
        //                 && (_Blocks[index - _Size.x].Id != _Blocks[index].Id))))
        //     {
        //         _Blocks[index].Faces.SetFace(Direction.South, true);
        //         AddTriangles(Direction.South, true);
        //
        //         int traversals;
        //         Vector3 uvSize = Vector3.one;
        //
        //         if (_AggressiveFaceMerging)
        //         {
        //             traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.South, Direction.East,
        //                 1, _Size.x, _Blocks[index].Id);
        //
        //             if (traversals > 1)
        //             {
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
        //                 uvSize.x = traversals;
        //             }
        //             else
        //             {
        //                 traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.South,
        //                     Direction.Up,
        //                     _VerticalIndexStep, _Size.y, _Blocks[index].Id);
        //
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
        //                 uvSize.z = traversals;
        //             }
        //         }
        //         else
        //         {
        //             AddVertices(Direction.South, localPosition);
        //         }
        //
        //         if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
        //             Direction.South, uvSize, out BlockUVs blockUVs))
        //         {
        //             _UVs.Add(blockUVs.BottomLeft);
        //             _UVs.Add(blockUVs.TopLeft);
        //             _UVs.Add(blockUVs.BottomRight);
        //             _UVs.Add(blockUVs.TopRight);
        //         }
        //     }
        //
        //
        //     if (!_Blocks[index].Faces.HasFace(Direction.West)
        //         && (((localPosition.x == 0)
        //              && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out blockId)
        //              && (_Blocks[index].Id != blockId))
        //             || ((localPosition.x > 0) && (_Blocks[index - 1].Id != _Blocks[index].Id))))
        //     {
        //         _Blocks[index].Faces.SetFace(Direction.West, true);
        //         AddTriangles(Direction.West, true);
        //
        //         int traversals;
        //         Vector3 uvSize = Vector3.one;
        //
        //         if (_AggressiveFaceMerging)
        //         {
        //             traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.West, Direction.North,
        //                 _Size.x, _Size.z, _Blocks[index].Id);
        //
        //             if (traversals > 1)
        //             {
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
        //                 uvSize.x = traversals;
        //             }
        //             else
        //             {
        //                 traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.West, Direction.Up,
        //                     _VerticalIndexStep, _Size.y, _Blocks[index].Id);
        //
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
        //                 uvSize.z = traversals;
        //             }
        //         }
        //         else
        //         {
        //             AddVertices(Direction.West, localPosition);
        //         }
        //
        //         if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
        //             Direction.West, uvSize, out BlockUVs blockUVs))
        //         {
        //             _UVs.Add(blockUVs.TopLeft);
        //             _UVs.Add(blockUVs.TopRight);
        //             _UVs.Add(blockUVs.BottomLeft);
        //             _UVs.Add(blockUVs.BottomRight);
        //         }
        //     }
        //
        //     if (!_Blocks[index].Faces.HasFace(Direction.Up)
        //         && ((localPosition.y == (_Size.y - 1))
        //             || ((localPosition.y < (_Size.y - 1))
        //                 && (_Blocks[index + _VerticalIndexStep].Id != _Blocks[index].Id))))
        //     {
        //         _Blocks[index].Faces.SetFace(Direction.Up, true);
        //         AddTriangles(Direction.Up, true);
        //
        //         int traversals;
        //         Vector3 uvSize = Vector3.one;
        //
        //         if (_AggressiveFaceMerging)
        //         {
        //             traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Up, Direction.North,
        //                 _Size.x, _Size.z, _Blocks[index].Id);
        //
        //             if (traversals > 1)
        //             {
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
        //                 uvSize.x = traversals;
        //             }
        //             else
        //             {
        //                 traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Up, Direction.East,
        //                     1, _Size.x, _Blocks[index].Id);
        //
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
        //                 uvSize.z = traversals;
        //             }
        //         }
        //         else
        //         {
        //             AddVertices(Direction.Up, localPosition);
        //         }
        //
        //         if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
        //             Direction.Up, uvSize, out BlockUVs blockUVs))
        //         {
        //             _UVs.Add(blockUVs.BottomLeft);
        //             _UVs.Add(blockUVs.BottomRight);
        //             _UVs.Add(blockUVs.TopLeft);
        //             _UVs.Add(blockUVs.TopRight);
        //         }
        //     }
        //
        //     // ignore the very bottom face of the world to reduce verts/tris
        //     if (!_Blocks[index].Faces.HasFace(Direction.Down)
        //         && (localPosition.y > 0)
        //         && (_Blocks[index - _VerticalIndexStep].Id != _Blocks[index].Id))
        //     {
        //         _Blocks[index].Faces.SetFace(Direction.Down, true);
        //         AddTriangles(Direction.Down, true);
        //
        //         int traversals;
        //         Vector3 uvSize = Vector3.one;
        //
        //         if (_AggressiveFaceMerging)
        //         {
        //             traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Down, Direction.North,
        //                 _Size.x, _Size.z, _Blocks[index].Id);
        //
        //             if (traversals > 1)
        //             {
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
        //                 uvSize.x = traversals;
        //             }
        //             else
        //             {
        //                 traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Down,
        //                     Direction.East, 1, _Size.x, _Blocks[index].Id);
        //
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
        //                 _Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
        //                 uvSize.z = traversals;
        //             }
        //         }
        //         else
        //         {
        //             AddVertices(Direction.Down, localPosition);
        //         }
        //
        //         if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
        //             Direction.Down, uvSize, out BlockUVs blockUVs))
        //         {
        //             _UVs.Add(blockUVs.BottomLeft);
        //             _UVs.Add(blockUVs.TopLeft);
        //             _UVs.Add(blockUVs.BottomRight);
        //             _UVs.Add(blockUVs.TopRight);
        //         }
        //     }
        // }

        private void TraverseIndex(int3 origin, int index, int3 localPosition)
        {
            int3 globalPosition = origin + localPosition;

            // ensure this block face hasn't already been traversed
            if (!_Mask[index].Faces.HasFace(Direction.North)
                // check if we're on the far edge of the chunk, and if so, query WorldController for blocks in adjacent chunk
                && (((localPosition.z == (_Size.z - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.North, out ushort blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    // however if we're inside the chunk, use the _Mask[] array index for check
                    || ((localPosition.z < (_Size.z - 1))
                        && BlockController.Current.CheckBlockHasProperties(_Mask[index + _Size.x].Id,
                            BlockDefinition.Property.Transparent))))
            {
                // set face of current block so it isn't traversed over
                _Mask[index].Faces.SetFace(Direction.North, true);
                // add triangles for this block face
                AddTriangles(Direction.North);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, _Size.x, Direction.North,
                        Direction.East, 1);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        _Vertices.Add(localPosition + new float3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new float3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new float3(traversals, 0f, 1f));
                        _Vertices.Add(localPosition + new float3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, localPosition.y, _Size.y, Direction.North,
                            Direction.Up, _VerticalIndexStep);

                        _Vertices.Add(localPosition + new float3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new float3(0f, traversals, 1f));
                        _Vertices.Add(localPosition + new float3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new float3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    // no traversals found, so just add vertices for a regular 1x1 face
                    AddVertices(Direction.North, localPosition);
                }

                // attempt to retrieve and add uvs for block face
                if (BlockController.Current.GetBlockSpriteUVs(_Mask[index].Id, globalPosition,
                    Direction.North, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.TopRight);
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.BottomRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.East)
                && (((localPosition.x == (_Size.x - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.East, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.x < (_Size.x - 1))
                        && BlockController.Current.CheckBlockHasProperties(_Mask[index + 1].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Mask[index].Faces.SetFace(Direction.East, true);
                AddTriangles(Direction.East);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, _Size.z, Direction.East,
                        Direction.North, _Size.x);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new float3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(1f, 0f, traversals));
                        _Vertices.Add(localPosition + new float3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new float3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, _Size.y, Direction.East,
                            Direction.Up, _VerticalIndexStep);

                        _Vertices.Add(localPosition + new float3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(1f, 0f, 1f));
                        _Vertices.Add(localPosition + new float3(1f, traversals, 0f));
                        _Vertices.Add(localPosition + new float3(1f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.East, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(_Mask[index].Id, globalPosition,
                    Direction.East, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.BottomRight);
                    _UVs.Add(blockUVs.TopRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.South, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.z > 0)
                        && BlockController.Current.CheckBlockHasProperties(_Mask[index - _Size.x].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Mask[index].Faces.SetFace(Direction.South, true);
                AddTriangles(Direction.South);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, _Size.x, Direction.South,
                        Direction.East, 1);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new float3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new float3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, _Size.y, Direction.South,
                            Direction.Up, _VerticalIndexStep);

                        _Vertices.Add(localPosition + new float3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new float3(1f, traversals, 0f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.South, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(_Mask[index].Id, globalPosition,
                    Direction.South, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.BottomRight);
                    _UVs.Add(blockUVs.TopRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.West, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.x > 0)
                        && BlockController.Current.CheckBlockHasProperties(_Mask[index - 1].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Mask[index].Faces.SetFace(Direction.West, true);
                AddTriangles(Direction.West);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, _Size.z, Direction.West,
                        Direction.North, _Size.x);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new float3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new float3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, _Size.y, Direction.West,
                            Direction.Up, _VerticalIndexStep);

                        _Vertices.Add(localPosition + new float3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, traversals, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new float3(0f, traversals, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.West, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(_Mask[index].Id, globalPosition,
                    Direction.West, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.TopRight);
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.BottomRight);
                }
            }

            if (!_Mask[index].Faces.HasFace(Direction.Up)
                && (((localPosition.y == (_Size.y - 1))
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.Up, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.y < (_Size.y - 1))
                        && BlockController.Current.CheckBlockHasProperties(_Mask[index + _VerticalIndexStep].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Mask[index].Faces.SetFace(Direction.Up, true);
                AddTriangles(Direction.Up);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, _Size.z, Direction.Up,
                        Direction.North, _Size.x);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new float3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new float3(1f, 1f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 1f, traversals));
                        _Vertices.Add(localPosition + new float3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, _Size.x, Direction.Up,
                            Direction.East, 1);

                        _Vertices.Add(localPosition + new float3(0f, 1f, 0f));
                        _Vertices.Add(localPosition + new float3(traversals, 1f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 1f, 1f));
                        _Vertices.Add(localPosition + new float3(traversals, 1f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Up, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(_Mask[index].Id, globalPosition,
                    Direction.Up, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.BottomRight);
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.TopRight);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!_Mask[index].Faces.HasFace(Direction.Down)
                && (((localPosition.y == 0)
                     && WorldController.Current.TryGetBlock(globalPosition + Directions.Down, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.y > 0)
                        && BlockController.Current.CheckBlockHasProperties(_Mask[index - _VerticalIndexStep].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Mask[index].Faces.SetFace(Direction.Down, true);
                AddTriangles(Direction.Down);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, _Size.z, Direction.Down,
                        Direction.North, _Size.x);

                    if (traversals > 1)
                    {
                        _Vertices.Add(localPosition + new float3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 0f, traversals));
                        _Vertices.Add(localPosition + new float3(1f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, _Size.x, Direction.Down,
                            Direction.East, 1);

                        _Vertices.Add(localPosition + new float3(0f, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(0f, 0f, 1f));
                        _Vertices.Add(localPosition + new float3(traversals, 0f, 0f));
                        _Vertices.Add(localPosition + new float3(traversals, 0f, 1f));
                        uvSize.z = traversals;
                    }
                }
                else
                {
                    AddVertices(Direction.Down, localPosition);
                }

                if (BlockController.Current.GetBlockSpriteUVs(_Mask[index].Id, globalPosition,
                    Direction.Down, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.BottomRight);
                    _UVs.Add(blockUVs.TopRight);
                }
            }
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

        private void AddVertices(Direction direction, int3 localPosition)
        {
            float3[] vertices = BlockFaces.Vertices.FaceVertices[direction];

            foreach (float3 vertex in vertices)
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
        /// <param name="limitingSliceValue">Maximum amount of traversals in given traversal direction.</param>
        /// <param name="traversalDirection">Direction to traverse in.</param>
        /// <param name="faceDirection">Direction to check faces while traversing.</param>
        /// <param name="traversalFactor">Amount of indexes to move forwards for each successful traversal in given direction.</param>
        /// <returns><see cref="int" /> representing how many successful traversals were made in the given traversal direction.</returns>
        private int GetTraversals(int index, int3 globalPosition, int slice, int limitingSliceValue,
            Direction faceDirection,
            Direction traversalDirection, int traversalFactor)
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
                // if we were incrementing on z, the factor would be _Size.x
                // and on y it would be (_Size.x * _Size.z)
                traversalIndex = index + (traversals * traversalFactor);

                if ((_Mask[index].Id != _Mask[traversalIndex].Id)
                    || _Mask[traversalIndex].Faces.HasFace(faceDirection)
                    // ensure the block adjacent to our current block is transparent
                    || !WorldController.Current.TryGetBlock(
                        globalPosition + (traversals * traversalNormal) + faceNormal, out ushort blockId)
                    || !BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
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
