#region

using System.Threading;
using Controllers.State;
using Controllers.World;
using Game.World.Blocks;
using UnityEngine;

// ReSharper disable TooWideLocalVariableScope

#endregion

namespace Game.World.Chunks
{
    public class ChunkMesher
    {
        private Bounds _Bounds;
        private Vector3 _Position;

        public CancellationToken AbortToken;

        public Bounds Bounds
        {
            get => _Bounds;
            set
            {
                _Bounds = value;
                _Position = _Bounds.min;
            }
        }

        public Block[] Blocks { get; set; }
        public MeshData MeshData { get; set; }

        public bool AggressiveFaceMerging;

        public ChunkMesher()
        {
        }

        public ChunkMesher(
            Bounds bounds, Block[] blocks, ref MeshData meshData, bool aggressiveFaceMerging,
            CancellationToken abortToken)
        {
            AbortToken = abortToken;
            Bounds = bounds;
            Blocks = blocks;
            MeshData = meshData;
            AggressiveFaceMerging = aggressiveFaceMerging;
            _Position = Bounds.min;
        }

        /// <summary>
        ///     Applies and returns processed <see cref="UnityEngine.Mesh" />.
        /// </summary>
        /// <param name="mesh">Given <see cref="UnityEngine.Mesh" /> to apply processed data to.</param>
        /// <returns>Processed <see cref="UnityEngine.Mesh" />.</returns>
        public void SetMesh(ref Mesh mesh)
        {
//            if ((_Vertices.Count == 0) || (_Triangles.Count == 0))
//            {
//                return;
//            }
//
//            mesh.Clear();
//
//            mesh.subMeshCount = 2;
//            mesh.indexFormat = _Vertices.Count > 65000
//                ? IndexFormat.UInt32
//                : IndexFormat.UInt16;
//
//            mesh.MarkDynamic();
//            mesh.SetVertices(_Vertices);
//            mesh.SetTriangles(_Triangles, 0);
//            mesh.SetTriangles(_TransparentTriangles, 1);
//
//            // check uvs count in case of no UVs to apply to mesh
//            if (_UVs.Count > 0)
//            {
//                mesh.SetUVs(0, _UVs);
//            }
//
//            mesh.RecalculateNormals();
//            mesh.RecalculateTangents();
//
//            //mesh.UploadMeshData(true);
        }

        public void GenerateMesh()
        {
            for (int index = 0; (index < Blocks.Length) && !AbortToken.IsCancellationRequested; index++)
            {
                if (Blocks[index].Id == BlockController.BLOCK_EMPTY_ID)
                {
                    continue;
                }

                Vector3Int localPosition = Mathv.GetIndexAsVector3Int(index, ChunkRegionController.Size);

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

            if (!Blocks[index].HasFace(Direction.North)
                && (((localPosition.z == (ChunkRegionController.Size.z - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.forward, out Block block)
                     && (block.Id != Blocks[index].Id))
                    || ((localPosition.z < (ChunkRegionController.Size.z - 1))
                        && (Blocks[index + ChunkRegionController.Size.x].Id != Blocks[index].Id))))
            {
                // todo fix northern transparent faces sometimes not culling inner faces

                // set face of current block so it isn't traversed over
                Blocks[index].SetFace(Direction.North, true);
                // add triangles for this block face
                AddTriangles(Direction.North, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.North, Direction.East,
                        1,
                        ChunkRegionController.Size.x, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.North,
                            Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y, Blocks[index].Id);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
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
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[3]);
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.East)
                && (((localPosition.x == (ChunkRegionController.Size.x - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out block)
                     && (Blocks[index].Id != block.Id))
                    || ((localPosition.x < (ChunkRegionController.Size.x - 1))
                        && (Blocks[index + 1].Id != Blocks[index].Id))))
            {
                Blocks[index].SetFace(Direction.East, true);
                AddTriangles(Direction.East, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.East, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.East, Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y, Blocks[index].Id);

                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }

            if (!Blocks[index].HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out block)
                     && (Blocks[index].Id != block.Id))
                    || ((localPosition.z > 0) && (Blocks[index - ChunkRegionController.Size.x].Id != Blocks[index].Id))))
            {
                Blocks[index].SetFace(Direction.South, true);
                AddTriangles(Direction.South, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.South, Direction.East,
                        1,
                        ChunkRegionController.Size.x, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.South,
                            Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y, Blocks[index].Id);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }


            if (!Blocks[index].HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out block)
                     && (Blocks[index].Id != block.Id))
                    || ((localPosition.x > 0) && (Blocks[index - 1].Id != Blocks[index].Id))))
            {
                Blocks[index].SetFace(Direction.West, true);
                AddTriangles(Direction.West, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.West, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.West, Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y, Blocks[index].Id);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
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
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[3]);
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.Up)
                && (((localPosition.y == (ChunkRegionController.Size.y - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.up, out block)
                     && (block.Id != Blocks[index].Id))
                    || ((localPosition.y < (ChunkRegionController.Size.y - 1))
                        && (Blocks[index + ChunkRegionController.YIndexStep].Id != Blocks[index].Id))))
            {
                Blocks[index].SetFace(Direction.Up, true);
                AddTriangles(Direction.Up, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Up, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Up, Direction.East,
                            1,
                            ChunkRegionController.Size.x, Blocks[index].Id);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!Blocks[index].HasFace(Direction.Down)
                && (localPosition.y > 0)
                && (Blocks[index - ChunkRegionController.YIndexStep].Id != Blocks[index].Id))
            {
                Blocks[index].SetFace(Direction.Down, true);
                AddTriangles(Direction.Down, true);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Down, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z, Blocks[index].Id);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Down,
                            Direction.East, 1,
                            ChunkRegionController.Size.x, Blocks[index].Id);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }
        }

        private void TraverseIndex(int index, Vector3Int localPosition)
        {
            Vector3 globalPosition = _Position + localPosition;

            // ensure this block face hasn't already been traversed
            if (!Blocks[index].HasFace(Direction.North)
                // check if we're on the far edge of the chunk, and if so, query WorldController for blocks in adjacent chunk
                && (((localPosition.z == (ChunkRegionController.Size.z - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.forward, out Block block)
                     && block.Transparent)
                    // however if we're inside the chunk, use the proper Blocks[] array index for check 
                    || ((localPosition.z < (ChunkRegionController.Size.z - 1))
                        && Blocks[index + ChunkRegionController.Size.x].Transparent)))
            {
                // set face of current block so it isn't traversed over
                Blocks[index].SetFace(Direction.North, true);
                // add triangles for this block face
                AddTriangles(Direction.North);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.North, Direction.East,
                        1,
                        ChunkRegionController.Size.x);

                    if (traversals > 1)
                    {
                        // The traversals value goes into the vertex points that have a positive value
                        // on the same axis as your slice value.
                        // So for instance, we were traversing on the x, so we'll be extending the x point of our
                        // vertices by the number of successful traversals.
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        // if traversal failed (no blocks found in probed direction) then look on next axis
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.North,
                            Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
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
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[3]);
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.East)
                && (((localPosition.x == (ChunkRegionController.Size.x - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.right, out block)
                     && block.Transparent)
                    || ((localPosition.x < (ChunkRegionController.Size.x - 1)) && Blocks[index + 1].Transparent)))
            {
                Blocks[index].SetFace(Direction.East, true);
                AddTriangles(Direction.East);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.East, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.East, Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y);

                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 1f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }

            if (!Blocks[index].HasFace(Direction.South)
                && (((localPosition.z == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.back, out block)
                     && block.Transparent)
                    || ((localPosition.z > 0) && Blocks[index - ChunkRegionController.Size.x].Transparent)))
            {
                Blocks[index].SetFace(Direction.South, true);
                AddTriangles(Direction.South);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.South, Direction.East,
                        1,
                        ChunkRegionController.Size.x);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.South,
                            Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, traversals, 0f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }

            if (!Blocks[index].HasFace(Direction.West)
                && (((localPosition.x == 0)
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.left, out block)
                     && block.Transparent)
                    || ((localPosition.x > 0) && Blocks[index - 1].Transparent)))
            {
                Blocks[index].SetFace(Direction.West, true);
                AddTriangles(Direction.West);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.West, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.y, Direction.West, Direction.Up,
                            ChunkRegionController.YIndexStep, ChunkRegionController.Size.y);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, traversals, 1f));
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
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[3]);
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[2]);
                }
            }

            if (!Blocks[index].HasFace(Direction.Up)
                && (((localPosition.y == (ChunkRegionController.Size.y - 1))
                     && WorldController.Current.TryGetBlockAt(globalPosition + Vector3.up, out block)
                     && block.Transparent)
                    || ((localPosition.y < (ChunkRegionController.Size.y - 1))
                        && Blocks[index + ChunkRegionController.YIndexStep].Transparent)))
            {
                Blocks[index].SetFace(Direction.Up, true);
                AddTriangles(Direction.Up);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Up, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 1f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Up, Direction.East,
                            1,
                            ChunkRegionController.Size.x);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 1f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 1f, 1f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }

            // ignore the very bottom face of the world to reduce verts/tris
            if (!Blocks[index].HasFace(Direction.Down)
                && (localPosition.y > 0)
                && Blocks[index - ChunkRegionController.YIndexStep].Transparent)
            {
                Blocks[index].SetFace(Direction.Down, true);
                AddTriangles(Direction.Down);

                int traversals;
                Vector3 uvSize = Vector3.one;

                if (AggressiveFaceMerging)
                {
                    traversals = GetTraversals(index, globalPosition, localPosition.z, Direction.Down, Direction.North,
                        ChunkRegionController.Size.x, ChunkRegionController.Size.z);

                    if (traversals > 1)
                    {
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, traversals));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(1f, 0f, traversals));
                        uvSize.x = traversals;
                    }
                    else
                    {
                        traversals = GetTraversals(index, globalPosition, localPosition.x, Direction.Down,
                            Direction.East, 1,
                            ChunkRegionController.Size.x);

                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(0f, 0f, 1f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 0f));
                        MeshData.Vertices.Add(localPosition + new Vector3(traversals, 0f, 1f));
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
                    MeshData.UVs.Add(uvs[0]);
                    MeshData.UVs.Add(uvs[1]);
                    MeshData.UVs.Add(uvs[2]);
                    MeshData.UVs.Add(uvs[3]);
                }
            }
        }

        private void AddTriangles(Direction direction, bool transparent = false)
        {
            foreach (int triangleValue in BlockFaces.Triangles.FaceTriangles[direction])
            {
                if (transparent)
                {
                    MeshData.TransparentTriangles.Add(MeshData.Vertices.Count + triangleValue);
                }
                else
                {
                    MeshData.Triangles.Add(MeshData.Vertices.Count + triangleValue);
                }
            }
        }

        private void AddVertices(Direction direction, Vector3 localPosition)
        {
            Vector3[] vertices = BlockFaces.Vertices.FaceVertices[direction];

            foreach (Vector3 vertex in vertices)
            {
                MeshData.Vertices.Add(vertex + localPosition);
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
