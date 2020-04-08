#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.Graphics;
using Wyd.System;

// ReSharper disable TooWideLocalVariableScope

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMesher
    {
        private readonly Stopwatch _Stopwatch;
        private readonly List<Vector3> _Vertices;
        private readonly List<int> _Triangles;
        private readonly List<int> _TransparentTriangles;
        private readonly List<Vector3> _UVs;

        private MeshBlock[] _Blocks;
        private int3 _Size;
        private int _VerticalIndexStep;
        private GenerationData _GenerationData;
        private CancellationToken _AbortToken;
        private bool _AggressiveFaceMerging;

        public TimeSpan SetBlockTimeSpan { get; private set; }
        public TimeSpan MeshingTimeSpan { get; private set; }

        public ChunkMesher()
        {
            _Stopwatch = new Stopwatch();
            _Blocks = new MeshBlock[0];
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Triangles = new List<int>();
            _TransparentTriangles = new List<int>();
        }

        public void SetRuntimeFields(GenerationData generationData, CancellationToken abortToken,
            bool aggressiveFaceMerging)
        {
            _GenerationData = generationData;
            _Size = WydMath.ToInt(_GenerationData.Volume.Size);
            _VerticalIndexStep = (int)(_GenerationData.Volume.Size.x * _GenerationData.Volume.Size.z);

            int sizeProduct = (int)WydMath.Product(_GenerationData.Volume.Size);
            if (_Blocks.Length != sizeProduct)
            {
                _Blocks = new MeshBlock[sizeProduct];
            }

            _AbortToken = abortToken;
            _AggressiveFaceMerging = aggressiveFaceMerging;
        }

        public void ClearExistingData()
        {
            SetBlockTimeSpan = MeshingTimeSpan = TimeSpan.Zero;
            _Stopwatch.Reset();
            _Vertices.Clear();
            _Triangles.Clear();
            _TransparentTriangles.Clear();
            _UVs.Clear();
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
        }

        private void SetBlockData(IEnumerable<ushort> blocks)
        {
            int index = 0;

            foreach (ushort id in blocks)
            {
                _Blocks[index].Id = id;
                _Blocks[index].Faces.ClearFaces();

                index += 1;
            }
        }

        public void GenerateMesh()
        {
            if (_GenerationData.Blocks.IsUniform && (_GenerationData.Blocks.Value == BlockController.AIR_ID))
            {
                return;
            }

            _Stopwatch.Restart();
            SetBlockData(_GenerationData.Blocks.GetAllData());
            _Stopwatch.Stop();
            SetBlockTimeSpan = _Stopwatch.Elapsed;

            _Stopwatch.Restart();
            int index = -1;
            foreach (MeshBlock block in _Blocks)
            {
                index += 1;

                if (_AbortToken.IsCancellationRequested)
                {
                    return;
                }

                if (block.Id == BlockController.AIR_ID)
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
                    TraverseIndex(WydMath.ToInt(_GenerationData.Volume.MinPoint), index, localPosition);
                }
            }

            _Stopwatch.Stop();
            MeshingTimeSpan = _Stopwatch.Elapsed;
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

        private void TraverseIndex(int3 position, int index, int3 localPosition)
        {
            int3 globalPosition = position + localPosition;

            // ensure this block face hasn't already been traversed
            if (!_Blocks[index].Faces.HasFace(Direction.North)
                // check if we're on the far edge of the chunk, and if so, query WorldController for blocks in adjacent chunk
                && (((localPosition.z == (_Size.z - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Directions.North, out ushort blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    // however if we're inside the chunk, use the proper Blocks[] array index for check
                    || ((localPosition.z < (_Size.z - 1))
                        && BlockController.Current.CheckBlockHasProperties(_Blocks[index + _Size.x].Id,
                            BlockDefinition.Property.Transparent))))
            {
                // set face of current block so it isn't traversed over
                _Blocks[index].Faces.SetFace(Direction.North, true);
                // add triangles for this block face
                AddTriangles(Direction.North);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.North, Direction.East,
                        1, _Size.x);

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
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.North,
                            Direction.Up, _VerticalIndexStep, _Size.y);

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
                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.North, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.TopRight);
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.BottomRight);
                }
            }

            if (!_Blocks[index].Faces.HasFace(Direction.East)
                && (((localPosition.x == (_Size.x - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Directions.East, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.x < (_Size.x - 1))
                        && BlockController.Current.CheckBlockHasProperties(_Blocks[index + 1].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Blocks[index].Faces.SetFace(Direction.East, true);
                AddTriangles(Direction.East);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.East, Direction.North,
                        _Size.x, _Size.z);

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
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.East, Direction.Up,
                            _VerticalIndexStep, _Size.y);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.East, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.BottomRight);
                    _UVs.Add(blockUVs.TopRight);
                }
            }

            if (!_Blocks[index].Faces.HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Directions.South, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.z > 0)
                        && BlockController.Current.CheckBlockHasProperties(_Blocks[index - _Size.x].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Blocks[index].Faces.SetFace(Direction.South, true);
                AddTriangles(Direction.South);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.South, Direction.East,
                        1, _Size.x);

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
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.South,
                            Direction.Up, _VerticalIndexStep, _Size.y);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.South, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.BottomRight);
                    _UVs.Add(blockUVs.TopRight);
                }
            }

            if (!_Blocks[index].Faces.HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Directions.West, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.x > 0)
                        && BlockController.Current.CheckBlockHasProperties(_Blocks[index - 1].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Blocks[index].Faces.SetFace(Direction.West, true);
                AddTriangles(Direction.West);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.West, Direction.North,
                        _Size.x, _Size.z);

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
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.West, Direction.Up,
                            _VerticalIndexStep, _Size.y);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.West, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.TopRight);
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.BottomRight);
                }
            }

            if (!_Blocks[index].Faces.HasFace(Direction.Up)
                && (((localPosition.y == (_Size.y - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Directions.Up, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.y < (_Size.y - 1))
                        && BlockController.Current.CheckBlockHasProperties(_Blocks[index + _VerticalIndexStep].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Blocks[index].Faces.SetFace(Direction.Up, true);
                AddTriangles(Direction.Up);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Up, Direction.North,
                        _Size.x, _Size.z);

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
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Up, Direction.East,
                            1, _Size.x);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
                    Direction.Up, uvSize, out BlockUVs blockUVs))
                {
                    _UVs.Add(blockUVs.BottomLeft);
                    _UVs.Add(blockUVs.BottomRight);
                    _UVs.Add(blockUVs.TopLeft);
                    _UVs.Add(blockUVs.TopRight);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!_Blocks[index].Faces.HasFace(Direction.Down)
                && (((localPosition.y == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Directions.Down, out blockId)
                     && BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    || ((localPosition.y > 0)
                        && BlockController.Current.CheckBlockHasProperties(_Blocks[index - _VerticalIndexStep].Id,
                            BlockDefinition.Property.Transparent))))
            {
                _Blocks[index].Faces.SetFace(Direction.Down, true);
                AddTriangles(Direction.Down);

                int traversals;
                float3 uvSize = new float3(1f);

                if (_AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Down, Direction.North,
                        _Size.x, _Size.z);

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
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Down,
                            Direction.East, 1, _Size.x);

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

                if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index].Id, globalPosition,
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
        /// <param name="traversalDirection">Direction to traverse in.</param>
        /// <param name="faceDirection">Direction to check faces while traversing.</param>
        /// <param name="traversalFactor">Amount of indexes to move forwards for each successful traversal in given direction.</param>
        /// <param name="limitingSliceValue">Maximum amount of traversals in given traversal direction.</param>
        /// <param name="id">Block ID of current block.</param>
        /// <returns><see cref="int" /> representing how many successful traversals were made in the given direction.</returns>
        private int GetTraversals(int index, int3 globalPosition, int slice, Direction faceDirection,
            Direction traversalDirection, int traversalFactor, int limitingSliceValue, int id = -1)
        {
            // 1 being the current block at `index`
            int traversals = 1;

            if (!_AggressiveFaceMerging)
            {
                return traversals;
            }

            // incrementing on x, so the traversal factor is 1
            // if we were incrementing on z, the factor would be _Size.x
            // and on y it would be (_Size.x * _Size.z)
            int traversalIndex = index + (traversals * traversalFactor);

            while ( // Set traversalIndex and ensure it is within the chunk's bounds
                ((slice + traversals) < limitingSliceValue)
                // This check removes the need to check if the adjacent block is transparent,
                // as our current block will never be transparent
                && (_Blocks[index].Id == _Blocks[traversalIndex].Id)
                && !_Blocks[traversalIndex].Faces.HasFace(faceDirection)
                // ensure the block to the north of our current block is transparent
                && WorldController.Current.TryGetBlockAt(
                    globalPosition + (traversals * traversalDirection.ToInt3()) + faceDirection.ToInt3(),
                    out ushort blockId)
                && (((id == -1)
                     && BlockController.Current.CheckBlockHasProperties(blockId,
                         BlockDefinition.Property.Transparent))
                    || ((id > -1) && (id != blockId))))
            {
                _Blocks[traversalIndex].Faces.SetFace(faceDirection, true);

                // increment and set traversal values
                traversals++;
                traversalIndex = index + (traversals * traversalFactor);
            }

            return traversals;
        }

        #endregion
    }
}
