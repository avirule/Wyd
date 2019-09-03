#region

using System;
using System.Collections.Generic;
using System.Linq;
using Controllers.Game;
using Controllers.World;
using Game;
using Game.World.Block;
using Game.World.Chunk;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Threading
{
    public class ChunkMeshingThreadedItem : ThreadedItem
    {
        private readonly List<int> _Triangles;
        private readonly List<Vector3> _Vertices;
        private readonly List<Vector3> _UVs;
        private readonly int _MaximumChunkAxisSize;
        private readonly byte[] _Faces;

        private Vector3 _Position;
        private ushort[] _Blocks;
        private ushort[][] _FirstMask;
        private ushort[][] _SecondMask;
        private bool _Greedy;

        /// <summary>
        ///     Initialises a new instance of the <see cref="ChunkMeshingThreadedItem" /> class.
        /// </summary>
        /// <seealso cref="ChunkBuildingThreadedItem" />
        public ChunkMeshingThreadedItem()
        {
            _Triangles = new List<int>();
            _Vertices = new List<Vector3>();
            _UVs = new List<Vector3>();
            _Faces = new byte[Chunk.Size.Product()];

            _MaximumChunkAxisSize = Math.Max(Math.Max(Chunk.Size.x, Chunk.Size.y), Chunk.Size.z);
            _FirstMask = _SecondMask = new ushort[_MaximumChunkAxisSize][];
        }

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        public void Set(Vector3 position, ushort[] blocks, bool greedy)
        {
            _Vertices.Clear();
            _Triangles.Clear();
            _UVs.Clear();

            _Position = position;
            _Blocks = blocks;
            _Greedy = greedy;
        }

        protected override void Process()
        {
            if (_Blocks == default)
            {
                return;
            }

            if (_Greedy)
            {
                GenerateGreedyMeshData();
            }
            else
            {
                GenerateNaiveMeshData();
            }
        }

        /// <summary>
        ///     Applies and returns processed <see cref="UnityEngine.Mesh" />.
        /// </summary>
        /// <param name="mesh">Given <see cref="UnityEngine.Mesh" /> to apply processed data to.</param>
        /// <returns>Processed <see cref="UnityEngine.Mesh" />.</returns>
        public void SetMesh(ref Mesh mesh)
        {
            if ((_Vertices.Count == 0) ||
                (_Triangles.Count == 0))
            {
                return;
            }

            if (mesh == default)
            {
                mesh = new Mesh();
            }
            else
            {
                mesh.Clear();
            }

            if (_Vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(_Vertices);
            mesh.SetTriangles(_Triangles, 0);

            // in case of no UVs to apply to mesh
            if (_UVs.Count > 0)
            {
                mesh.SetUVs(0, _UVs);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        private void GenerateNaiveMeshData()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
                if (AbortToken.IsCancellationRequested)
                {
                    return;
                }

                if (_Blocks[index] == BlockController.BLOCK_EMPTY_ID)
                {
                    continue;
                }

                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);
                Vector3 localPosition = new Vector3(x, y, z);
                Vector3 globalPosition = _Position + new Vector3(x, y, z);
                Vector3 uvSize = new Vector3(1f, 0, 1f);

                if (((z == (Chunk.Size.z - 1)) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.forward))) ||
                    ((z < (Chunk.Size.z - 1)) &&
                     BlockController.Current.IsBlockTransparent(_Blocks[index + Chunk.Size.x])))
                {
                    AddTriangles(Direction.North);
                    AddVertices(Direction.North, localPosition);
                    
                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.North, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                    }
                }

                if (((x == (Chunk.Size.x - 1)) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.right))) ||
                    ((x < (Chunk.Size.x - 1)) && BlockController.Current.IsBlockTransparent(_Blocks[index + 1])))
                {
                    AddTriangles(Direction.East);
                    AddVertices(Direction.East, localPosition);
                    
                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.East, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if (((z == 0) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.back))) ||
                    ((z > 0) && BlockController.Current.IsBlockTransparent(_Blocks[index - Chunk.Size.x])))
                {
                    AddTriangles(Direction.South);
                    AddVertices(Direction.South, localPosition);
                    
                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.South, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if (((x == 0) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.left))) ||
                    ((x > 0) && BlockController.Current.IsBlockTransparent(_Blocks[index - 1])))
                {
                    AddTriangles(Direction.West);
                    AddVertices(Direction.West, localPosition);
                    
                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.West, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                    }
                }

                if (((y == (Chunk.Size.y - 1)) &&
                     BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.up))) ||
                    ((y < (Chunk.Size.y - 1)) &&
                     BlockController.Current.IsBlockTransparent(_Blocks[index + (Chunk.Size.x * Chunk.Size.z)])))
                {
                    AddTriangles(Direction.Up);
                    AddVertices(Direction.Up, localPosition);

                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.Up, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[3]);
                    }
                }

                if ((y > 0) && BlockController.Current.IsBlockTransparent(
                        _Blocks[index - (Chunk.Size.x * Chunk.Size.z)]))
                {
                    AddTriangles(Direction.Down);
                    AddVertices(Direction.Down, localPosition);
                    
                    if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], globalPosition,
                        Direction.Down, uvSize, out Vector3[] uvs))
                    {
                        _UVs.Add(uvs[0]);
                        _UVs.Add(uvs[1]);
                        _UVs.Add(uvs[2]);
                        _UVs.Add(uvs[3]);
                    }
                }
            }
        }

        private void GenerateGreedyMeshData()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

                if (BlockController.Current.IsBlockTransparent(_Blocks[index]))
                {
                    _Faces[index] = 0;
                    continue;
                }

                Vector3 globalPosition = _Position + new Vector3(x, y, z);

                if (((z == (Chunk.Size.z - 1)) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.forward))) ||
                    ((z < (Chunk.Size.z - 1)) && BlockController.Current.IsBlockTransparent(
                         _Blocks[index + Chunk.Size.x])))
                {
                    _Faces[index] |= (byte) Direction.North;
                }

                if (((x == (Chunk.Size.x - 1)) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.right))) ||
                    ((x < (Chunk.Size.x - 1)) && BlockController.Current.IsBlockTransparent(
                         _Blocks[index + 1])))
                {
                    _Faces[index] |= (byte) Direction.East;
                }

                if (((z == 0) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.back))) ||
                    ((z > 0) && BlockController.Current.IsBlockTransparent(
                         _Blocks[index - Chunk.Size.x])))
                {
                    _Faces[index] |= (byte) Direction.South;
                }

                if (((x == 0) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.left))) ||
                    ((x > 0) && BlockController.Current.IsBlockTransparent(
                         _Blocks[index - 1])))
                {
                    _Faces[index] |= (byte) Direction.West;
                }

                if (((y == (Chunk.Size.y - 1)) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.up))) ||
                    ((y < (Chunk.Size.y - 1)) && BlockController.Current.IsBlockTransparent(
                         _Blocks[index + (Chunk.Size.x * Chunk.Size.z)])))
                {
                    _Faces[index] |= (byte) Direction.Up;
                }

                if (((y == 0) && BlockController.Current.IsBlockTransparent(
                         WorldController.Current.GetBlockAt(globalPosition + Vector3.down))) ||
                    ((y > 0) && BlockController.Current.IsBlockTransparent(
                         _Blocks[index - (Chunk.Size.x * Chunk.Size.z)])))
                {
                    _Faces[index] |= (byte) Direction.Down;
                }
            }

            
            // initialise mask arrays
            for (int i = 0; i < _FirstMask.Length; i++)
            {
                _FirstMask[i] = new ushort[_MaximumChunkAxisSize];
            }

            for (int i = 0; i < _SecondMask.Length; i++)
            {
                _SecondMask[i] = new ushort[_MaximumChunkAxisSize];
            }
            
            
            for (int x = 0; x < Chunk.Size.x; x++)
            {
                for (int y = 0; y < Chunk.Size.y; y++)
                {
                    for (int z = 0; z < Chunk.Size.z; z++)
                    {
                        int index = Index3DTo1D(x, y, z, Chunk.Size);

                        if ((_Faces[index] & (byte) Direction.West) != 0)
                        {
                            _FirstMask[z][y] = _Blocks[index];
                        }
                        else
                        {
                            _FirstMask[z][y] = BlockController.BLOCK_EMPTY_ID;
                        }

                        if ((_Faces[index] & (byte) Direction.East) != 0)
                        {
                            _SecondMask[z][y] = _Blocks[index];
                        }
                        else
                        {
                            _SecondMask[z][y] = BlockController.BLOCK_EMPTY_ID;
                        }
                    }
                }
                
                FilLWithQuads(_FirstMask, x, Direction.West);
                FilLWithQuads(_SecondMask, x + 1, Direction.East);
            }

            for (int y = 0; y < Chunk.Size.y; y++)
            {
                for (int x = 0; x < Chunk.Size.x; x++)
                {
                    for (int z = 0; z < Chunk.Size.z; z++)
                    {
                        int index = Index3DTo1D(x, y, z, Chunk.Size);

                        if ((_Faces[index] & (byte) Direction.Down) != 0)
                        {
                            _FirstMask[x][z] = _Blocks[index];
                        }
                        else
                        {
                            _FirstMask[x][z] = BlockController.BLOCK_EMPTY_ID;
                        }

                        if ((_Faces[index] & (byte) Direction.Up) != 0)
                        {
                            _SecondMask[x][z] = _Blocks[index];
                        }
                        else
                        {
                            _SecondMask[x][z] = BlockController.BLOCK_EMPTY_ID;
                        }
                    }
                }
                
                FilLWithQuads(_FirstMask, y, Direction.Down);
                FilLWithQuads(_SecondMask, y + 1, Direction.Up);
            }

            for (int z = 0; z < Chunk.Size.z; z++)
            {
                for (int x = 0; x < Chunk.Size.x; x++)
                {
                    for (int y = 0; y < Chunk.Size.y; y++)
                    {
                        int index = Index3DTo1D(x, y, z, Chunk.Size);

                        if ((_Faces[index] & (byte) Direction.South) != 0)
                        {
                            _FirstMask[x][y] = _Blocks[index];
                        }
                        else
                        {
                            _FirstMask[x][y] = BlockController.BLOCK_EMPTY_ID;
                        }

                        if ((_Faces[index] & (byte) Direction.North) != 0)
                        {
                            _SecondMask[x][y] = _Blocks[index];
                        }
                        else
                        {
                            _SecondMask[x][y] = _Blocks[index];
                        }
                    }
                }
                
                FilLWithQuads(_FirstMask, z, Direction.South);
                FilLWithQuads(_SecondMask, z + 1, Direction.North);
            }
        }

        private void FilLWithQuads(ushort[][] mask, int index, Direction direction)
        {
            for (int x = 0; x < mask.Length; x++)
            {
                ushort[] row = mask[x];

                for (int y = 0; y < row.Length; y++)
                {
                    if (row[y] == BlockController.BLOCK_EMPTY_ID)
                    {
                        continue;
                    }

                    int currentBlockId = row[y];
                    
                    int endY = FindEndY(row, currentBlockId, y);
                    int endX = FindEndX(mask, currentBlockId, y, endY, x);

                    y = endX - 1;

                    switch (direction)
                    {
                        case Direction.North:
                            CreateFaceNorth(index, x, y, endX, endY, index);
                            break;
                        case Direction.East:
                            CreateFaceEast(index, x, y, endX, endY, index);
                            break;
                        case Direction.South:
                            CreateFaceSouth(index, x, y, endX, endY, index);
                            break;
                        case Direction.West:
                            CreateFaceWest(index, x, y, endX, endY, index);
                            break;
                        case Direction.Up:
                            CreateFaceUp(index, x, y, endX, endY, index);
                            break;
                        case Direction.Down:
                            CreateFaceDown(index, x, y, endX, endY, index);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                    }
                }
            }
        }

        private void CreateFaceNorth(int index, float x, float y, float x2, float y2, float z)
        {
            AddTriangles(Direction.North);
            
            _Vertices.Add(new Vector3(x, y, z));
            _Vertices.Add(new Vector3(x2, y, z));
            _Vertices.Add(new Vector3(x2, y2, z));
            _Vertices.Add(new Vector3(x, y2, z));
            
            if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], new Vector3(x, y, z), 
                Direction.North, Vector3.one, out Vector3[] uvs))
            {
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[3]);
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[2]);
            }
        }

        private void CreateFaceEast(int index, float z, float y, float z2, float y2, float x)
        {
            AddTriangles(Direction.East);
            
            _Vertices.Add(new Vector3(x, y, z2));
            _Vertices.Add(new Vector3(x, y, z));
            _Vertices.Add(new Vector3(x, y2, z));
            _Vertices.Add(new Vector3(x, y2, z2));
            
            if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], new Vector3(x, y, z),
                Direction.East, Vector3.one, out Vector3[] uvs))
            {
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[2]);
                _UVs.Add(uvs[3]);
            }
        }

        private void CreateFaceSouth(int index, float x, float y, float x2, float y2, float z)
        {
            AddTriangles(Direction.South);
            
            _Vertices.Add(new Vector3(x2, y, z));
            _Vertices.Add(new Vector3(x, y, z));
            _Vertices.Add(new Vector3(x, y2, z));
            _Vertices.Add(new Vector3(x2, y2, z));
            
            if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], new Vector3(x, y, z),
                Direction.South, Vector3.one, out Vector3[] uvs))
            {
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[2]);
                _UVs.Add(uvs[3]);
            }
        }

        private void CreateFaceWest(int index, float z, float y, float z2, float y2, float x)
        {
            AddTriangles(Direction.West);
            
            _Vertices.Add(new Vector3(x, y, z));
            _Vertices.Add(new Vector3(x, y, z2));
            _Vertices.Add(new Vector3(x, y2, z2));
            _Vertices.Add(new Vector3(x, y2, z));
            
            if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], new Vector3(x, y, z),
                Direction.West, Vector3.one, out Vector3[] uvs))
            {
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[3]);
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[2]);
            }
        }

        private void CreateFaceUp(int index, float x, float z, float x2, float z2, float y)
        {
            AddTriangles(Direction.Up);
            
            _Vertices.Add(new Vector3(x, y, z2));
            _Vertices.Add(new Vector3(x2, y, z2));
            _Vertices.Add(new Vector3(x2, y, z));
            _Vertices.Add(new Vector3(x, y, z));
            
            if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], new Vector3(x, y, z),
                Direction.Up, Vector3.one, out Vector3[] uvs))
            {
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[2]);
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[3]);
            }
        }

        private void CreateFaceDown(int index, float x, float z, float x2, float z2, float y)
        {
            AddTriangles(Direction.Down);
            
            _Vertices.Add(new Vector3(x2, y, z2));
            _Vertices.Add(new Vector3(x, y, z2));
            _Vertices.Add(new Vector3(x, y, z));
            _Vertices.Add(new Vector3(x2, y, z));
            
            if (BlockController.Current.GetBlockSpriteUVs(_Blocks[index], new Vector3(x, y, z),
                Direction.Down, Vector3.one, out Vector3[] uvs))
            {
                _UVs.Add(uvs[0]);
                _UVs.Add(uvs[1]);
                _UVs.Add(uvs[2]);
                _UVs.Add(uvs[3]);
            }
        }

        private void AddTriangles(Direction direction)
        {
            int[] triangles = BlockFaces.Triangles.Get(direction);

            foreach (int triangleValue in triangles)
            {
                _Triangles.Add(_Vertices.Count + triangleValue);
            }
        }

        private void AddVertices(Direction direction, Vector3 localPosition)
        {
            Vector3[] vertices = BlockFaces.Vertices.Get(direction);

            foreach (Vector3 vertex in vertices)
            {
                _Vertices.Add(vertex + localPosition);
            }
        }
        
        private static int FindEndX(ushort[][] mask, int currentBlockId, int x, int startY, int endY)
        {
            int end = x + 1;

            while (end < mask.Length)
            {
                for (int checkY = startY; checkY < endY; checkY++)
                {
                    if (mask[end][checkY] != currentBlockId)
                    {
                        return end;
                    }
                }

                for (int checkY = startY; checkY < endY; checkY++)
                {
                    mask[end][checkY] = 0;
                }

                end++;
            }

            return end;
        }

        private static int FindEndY(ushort[] row, int currentBlockId, int startY)
        {
            int end = startY;

            while (end < row.Length && row[end] == currentBlockId)
            {
                row[end] = 0;
                end++;
            }

            return end;
        }

        public static int Index3DTo1D(int x, int y, int z, Vector3Int size3d)
        {
            return x + (z * size3d.x) + (y * size3d.x * size3d.z);
        }
    }
}