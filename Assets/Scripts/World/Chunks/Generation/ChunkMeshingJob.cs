#region

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentAsyncScheduler;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Collections;
using Wyd.Controllers.State;
using Wyd.Singletons;
using Wyd.World.Blocks;
using Debug = System.Diagnostics.Debug;

#endregion

namespace Wyd.World.Chunks.Generation
{
    public class ChunkMeshingJob : AsyncParallelJob
    {
        private const int _VERTEXES_ARRAY_SIZE = 4 * 6 * GenerationConstants.CHUNK_SIZE_CUBED * 2;
        private const int _TRIANGLES_ARRAY_SIZE = (int)((_VERTEXES_ARRAY_SIZE / 2f) * 1.5f);

        private static readonly VertexAttributeDescriptor[] _Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.SInt32, 1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.SInt32, 1)
        };

        private static readonly ArrayPool<MeshingBlock> _MeshingBlocksPool =
            ArrayPool<MeshingBlock>.Create(GenerationConstants.CHUNK_SIZE_CUBED, AsyncJobScheduler.MaximumConcurrentJobs);

        private static readonly ArrayPool<int> _VertexesArrayPool =
            ArrayPool<int>.Create(_VERTEXES_ARRAY_SIZE, AsyncJobScheduler.MaximumConcurrentJobs);

        private static readonly ArrayPool<int> _TrianglesArrayPool =
            ArrayPool<int>.Create(_TRIANGLES_ARRAY_SIZE, AsyncJobScheduler.MaximumConcurrentJobs);

        private static readonly SemaphoreSlim _MeshDataResourceLock = new SemaphoreSlim(AsyncJobScheduler.MaximumConcurrentJobs,
            AsyncJobScheduler.MaximumConcurrentJobs);

        private readonly Stopwatch _RuntimeStopwatch;
        private readonly bool _AdvancedMeshing;
        private readonly INodeCollection<ushort> _BlocksCollection;
        private readonly INodeCollection<ushort>[] _NeighborBlocksCollections;

        private MeshingBlock[] _MeshingBlocks;
        private TimeSpan _MeshingTimeSpan;

        private TimeSpan _PreMeshingTimeSpan;
        private int[] _Triangles;
        private int _TrianglesCount;
        private int[] _Vertexes;
        private int _VertexesCount;

        /// <summary>
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation indication.</param>
        /// <param name="blocksCollection"><see cref="INodeCollection{T}" /> of blocksCollection contained within the chunk.</param>
        /// <param name="neighborBlocksCollections"></param>
        /// <param name="advancedMeshing">Indicates whether to merge similarly textured faces.</param>
        public ChunkMeshingJob(CancellationToken cancellationToken, INodeCollection<ushort> blocksCollection,
            INodeCollection<ushort>[] neighborBlocksCollections, bool advancedMeshing)
            : base(GenerationConstants.CHUNK_SIZE_CUBED, Options.Instance.NaiveMeshingGroupSize)
        {
            LinkCancellationToken(cancellationToken);

            _RuntimeStopwatch = new Stopwatch();
            _NeighborBlocksCollections = neighborBlocksCollections;
            _NeighborBlocksCollections = new INodeCollection<ushort>[6];
            _BlocksCollection = blocksCollection;
            _AdvancedMeshing = advancedMeshing;
        }


        public void ApplyMeshData(ref Mesh mesh)
        {
            // 'is object' to bypass unity lifetime check for null
            if (!(mesh is object))
            {
                throw new NullReferenceException(nameof(mesh));
            }
            else
            {
                mesh.Clear();
            }

            if ((_VertexesCount == 0) || (_TrianglesCount == 0))
            {
                return;
            }

            if ((int)((_VertexesCount / 2f) * 1.5f) != _TrianglesCount)
            {
                throw new ArgumentOutOfRangeException($"Sum of all {_Triangles} should be 1.5x as many vertices.");
            }

            const MeshUpdateFlags default_flags = MeshUpdateFlags.DontRecalculateBounds
                                                  | MeshUpdateFlags.DontValidateIndices
                                                  | MeshUpdateFlags.DontResetBoneBounds;

            mesh.SetVertexBufferParams(_VertexesCount, _Layout);
            mesh.SetVertexBufferData(_Vertexes, 0, 0, _VertexesCount, 0, default_flags);

            // todo support for more submeshes, i.e. transparency
            mesh.subMeshCount = 1;
            mesh.SetIndexBufferParams(_TrianglesCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(_Triangles, 0, 0, _TrianglesCount, default_flags);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, _TrianglesCount), default_flags);

            mesh.bounds = new Bounds(new float3(GenerationConstants.CHUNK_SIZE / 2), new float3(GenerationConstants.CHUNK_SIZE));
        }


        #region AsyncJob Overrides

        protected override async Task Process()
        {
            Debug.Assert(_BlocksCollection != null);

            if (_BlocksCollection.IsUniform && (_BlocksCollection.Value == BlockController.AirID))
            {
                return;
            }

            _RuntimeStopwatch.Restart();

            await PrepareMeshing().ConfigureAwait(false);

            _RuntimeStopwatch.Stop();

            _PreMeshingTimeSpan = _RuntimeStopwatch.Elapsed;

            await _MeshDataResourceLock.WaitAsync().ConfigureAwait(false);

            _Vertexes = _VertexesArrayPool.Rent(_VERTEXES_ARRAY_SIZE);
            _Triangles = _TrianglesArrayPool.Rent(_TRIANGLES_ARRAY_SIZE);

            _RuntimeStopwatch.Restart();

            if (_AdvancedMeshing)
            {
                GenerateTraversalMesh();
            }
            else
            {
                await BatchTasksAndAwaitAll().ConfigureAwait(false);
            }

            _MeshingBlocksPool.Return(_MeshingBlocks, true);

            _RuntimeStopwatch.Stop();

            _MeshingTimeSpan = _RuntimeStopwatch.Elapsed;
        }

        protected override void ProcessIndex(int index)
        {
            if (_CancellationToken.IsCancellationRequested)
            {
                return;
            }

            ushort currentBlockId = _MeshingBlocks[index].ID;

            if (currentBlockId == BlockController.AirID)
            {
                return;
            }

            int localPosition = CompressVertex(WydMath.IndexTo3D(index, GenerationConstants.CHUNK_SIZE));

            bool transparentTraversal = BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent);

            //NaiveMeshIndex(index, localPosition, currentBlockId, transparentTraversal);
        }

        protected override Task ProcessFinished()
        {
            if (!_CancellationToken.IsCancellationRequested)
            {
                Singletons.Diagnostics.Instance["ChunkPreMeshing"].Enqueue(_PreMeshingTimeSpan);
                Singletons.Diagnostics.Instance["ChunkMeshing"].Enqueue(_MeshingTimeSpan);
            }

            return Task.CompletedTask;
        }

        #endregion


        #region Mesh Generation

        private async Task PrepareMeshing()
        {
            Debug.Assert(_BlocksCollection != null, $"{nameof(_BlocksCollection)} should not be null when meshing is started.");
            Debug.Assert(_NeighborBlocksCollections != null, $"{nameof(_NeighborBlocksCollections)} should not be null when meshing is started.");
            Debug.Assert(_NeighborBlocksCollections.Length == 6,
                $"{nameof(_NeighborBlocksCollections)} should have a length of 6, one for each neighboring chunk.");

            _MeshingBlocks = _MeshingBlocksPool.Rent(GenerationConstants.CHUNK_SIZE_CUBED);

            int index = 0;
            for (int y = 0; y < GenerationConstants.CHUNK_SIZE; y++)
            for (int z = 0; z < GenerationConstants.CHUNK_SIZE; z++)
            for (int x = 0; x < GenerationConstants.CHUNK_SIZE; x++, index++)
            {
                _MeshingBlocks[index].ID = ((Octree)_BlocksCollection).GetPoint(x, y, z);
            }
        }

        public void ReleaseResources()
        {
            if (_Vertexes != null)
            {
                _VertexesArrayPool.Return(_Vertexes);
                _Vertexes = null;
            }

            if (_Triangles != null)
            {
                _TrianglesArrayPool.Return(_Triangles);
                _Triangles = null;
            }

            _MeshDataResourceLock.Release();
        }

        /// <summary>
        ///     Generates the mesh data.
        /// </summary>
        private void GenerateTraversalMesh()
        {
            Debug.Assert(_MeshingBlocks.Length == GenerationConstants.CHUNK_SIZE_CUBED, $"{_MeshingBlocks} should be the same length as chunk data.");

            int index = 0;

            for (int y = 0; y < GenerationConstants.CHUNK_SIZE; y++)
            for (int z = 0; z < GenerationConstants.CHUNK_SIZE; z++)
            for (int x = 0; x < GenerationConstants.CHUNK_SIZE; x++, index++)
            {
                if (_CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ushort currentBlockId = _MeshingBlocks[index].ID;

                if (currentBlockId == BlockController.AirID)
                {
                    continue;
                }

                int localPosition = x | (y << GenerationConstants.CHUNK_SIZE_BIT_SHIFT) | (z << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2));

                TraverseIndex(index, localPosition, currentBlockId,
                    BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent));
            }
        }

        /// <summary>
        ///     Traverse given index of <see cref="_MeshingBlocks" /> to conditionally emit vertex data for each face.
        /// </summary>
        /// <param name="index">Current working index.</param>
        /// <param name="localPosition">3D projected local position of the current working index.</param>
        /// <param name="currentBlockId">Block ID present at the current working index.</param>
        /// <param name="isCurrentBlockTransparent">Whether or not this traversal uses transparent-specific conditionals.</param>
        private void TraverseIndex(int index, int localPosition, ushort currentBlockId, bool isCurrentBlockTransparent)
        {
            Debug.Assert(currentBlockId != BlockController.AirID, $"{nameof(TraverseIndex)} should not run on air blocks.");
            Debug.Assert((index >= 0) && (index < GenerationConstants.CHUNK_SIZE_CUBED), $"{nameof(index)} is not within chunk bounds.");
            Debug.Assert(WydMath.PointToIndex(DecompressVertex(localPosition), GenerationConstants.CHUNK_SIZE) == index,
                $"{nameof(localPosition)} does not match given {nameof(index)}.");
            Debug.Assert(_MeshingBlocks[index].ID == currentBlockId, $"{currentBlockId} is not equal to block ID at given index.");
            Debug.Assert(
                BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent) == isCurrentBlockTransparent,
                $"Given transparency state for {nameof(currentBlockId)} does not match actual block transparency.");

            // iterate once over all 6 faces of given cubic space
            for (int normalIndex = 0; normalIndex < 6; normalIndex++)
            {
                // face direction always exists on a single bit, so shift 1 by the current normalIndex (0-5)
                Direction faceDirection = (Direction)(1 << normalIndex);

                // check if current index has face already
                if (_MeshingBlocks[index].HasFace(faceDirection))
                {
                    continue;
                }

                // indicates whether the current face checking direction is negative or positive
                bool isNegativeFace = (normalIndex - 3) >= 0;
                // normalIndex constrained to represent the 3 axes
                int iModulo3 = normalIndex % 3;
                int iModulo3Shift = GenerationConstants.CHUNK_SIZE_BIT_SHIFT * iModulo3;
                // axis value of the current face check direction
                // example: for iteration normalIndex == 0—which is positive X—it'd be equal to localPosition.x
                int faceCheckAxisValue = (localPosition >> iModulo3Shift) & GenerationConstants.CHUNK_SIZE_BIT_MASK;
                // indicates whether or not the face check is within the current chunk bounds
                bool isFaceCheckOutOfBounds = (!isNegativeFace && (faceCheckAxisValue == (GenerationConstants.CHUNK_SIZE - 1)))
                                              || (isNegativeFace && (faceCheckAxisValue == 0));
                // total number of successful traversals
                int traversals = 0;

                for (int perpendicularNormalIndex = 1; perpendicularNormalIndex < 3; perpendicularNormalIndex++)
                {
                    // the index of the int3 traversalNormal to traverse on
                    int traversalNormalIndex = (iModulo3 + perpendicularNormalIndex) % 3;
                    int traversalNormalShift = GenerationConstants.CHUNK_SIZE_BIT_SHIFT * traversalNormalIndex;

                    // current value of the local position by traversal direction
                    int traversalNormalAxisValue = (localPosition >> traversalNormalShift) & GenerationConstants.CHUNK_SIZE_BIT_MASK;
                    // amount by integer to add to current index to get 3D->1D position of traversal position
                    int traversalIndexStep = GenerationConstants.IndexStepByNormalIndex[traversalNormalIndex];
                    // current traversal index, which is increased by traversalIndexStep every iteration the for loop below
                    int traversalIndex = index + (traversals * traversalIndexStep);
                    // local start axis position + traversals
                    int totalTraversalLength = traversalNormalAxisValue + traversals;

                    for (;
                        (totalTraversalLength < GenerationConstants.CHUNK_SIZE)
                        && !_MeshingBlocks[traversalIndex].HasFace(faceDirection)
                        && (_MeshingBlocks[traversalIndex].ID == currentBlockId);
                        totalTraversalLength++,
                        traversals++, // increment traversals
                        traversalIndex += traversalIndexStep) // increment traversal index by index step to adjust local working position
                    {
                        // check if current facing block axis value is within the local chunk
                        if (!isFaceCheckOutOfBounds)
                        {
                            // amount by integer to add to current traversal index to get 3D->1D position of facing block
                            int facedBlockIndex = traversalIndex + GenerationConstants.IndexStepByNormalIndex[normalIndex];
                            // if so, index into block ids and set facingBlockId
                            ushort facedBlockId = _MeshingBlocks[facedBlockIndex].ID;

                            // if transparent, traverse so long as facing block is not the same block id
                            // if opaque, traverse so long as facing block is transparent
                            if (isCurrentBlockTransparent)
                            {
                                if (currentBlockId == facedBlockId)
                                {
                                    break;
                                }
                            }
                            else if (!BlockController.Current.CheckBlockHasProperty(facedBlockId, BlockDefinition.Property.Transparent))
                            {
                                if (!isNegativeFace)
                                {
                                    // we've culled this face, and faced block is opaque as well, so cull it's face inverse to the current.
                                    Direction inverseFaceDirection = (Direction)(1 << ((normalIndex + 3) % 6));
                                    _MeshingBlocks[facedBlockIndex].SetFace(inverseFaceDirection);
                                }

                                break;
                            }
                        }
                        else
                        {
                            // this block of code translates the integer local position to the local position of the neighbor at [normalIndex]
                            int sign = isNegativeFace ? -1 : 1;
                            int iModuloComponentMask = GenerationConstants.CHUNK_SIZE_BIT_MASK << iModulo3Shift;
                            int translatedLocalPosition = localPosition + (traversals << traversalNormalShift);
                            int finalLocalPosition = (~iModuloComponentMask & translatedLocalPosition)
                                                     | (WydMath.Wrap(((translatedLocalPosition & iModuloComponentMask) >> iModulo3Shift) + sign,
                                                            GenerationConstants.CHUNK_SIZE, 0, GenerationConstants.CHUNK_SIZE - 1)
                                                        << iModulo3Shift);

                            // index into neighbor blocks collections, call .GetPoint() with adjusted local position
                            // remark: if there's no neighbor at the index given, then no chunk exists there (for instance,
                            //     chunks at the edge of render distance). In this case, return NullID so no face is rendered on edges.
                            ushort facedBlockId = _NeighborBlocksCollections[normalIndex]?.GetPoint(DecompressVertex(finalLocalPosition))
                                                  ?? BlockController.NullID;

                            if (isCurrentBlockTransparent)
                            {
                                if (currentBlockId == facedBlockId)
                                {
                                    break;
                                }
                            }
                            else if (!BlockController.Current.CheckBlockHasProperty(facedBlockId, BlockDefinition.Property.Transparent))
                            {
                                break;
                            }
                        }

                        _MeshingBlocks[traversalIndex].SetFace(faceDirection);
                    }

                    // if it's the first traversal and we've only made a 1x1x1 face, continue to test next axis
                    if ((traversals == 1) && (perpendicularNormalIndex == 1))
                    {
                        continue;
                    }

                    if ((traversals == 0) || !BlockController.Current.GetUVs(currentBlockId, faceDirection, out ushort textureId))
                    {
                        break;
                    }

                    // add triangles
                    int verticesCount = _VertexesCount / 2;

                    _Triangles[_TrianglesCount + 0] = verticesCount + 0;
                    _Triangles[_TrianglesCount + 1] = verticesCount + 2;
                    _Triangles[_TrianglesCount + 2] = verticesCount + 1;
                    _Triangles[_TrianglesCount + 3] = verticesCount + 2;
                    _Triangles[_TrianglesCount + 4] = verticesCount + 3;
                    _Triangles[_TrianglesCount + 5] = verticesCount + 1;
                    _TrianglesCount += 6;

                    int uvShift = (iModulo3 + traversalNormalIndex) % 2;
                    int compressedUv = (textureId << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2))
                                       | (1 << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * ((uvShift + 1) % 2)))
                                       | (traversals << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * uvShift));

                    int traversalShiftedMask = GenerationConstants.CHUNK_SIZE_BIT_MASK << traversalNormalShift;
                    int unaryTraversalShiftedMask = ~traversalShiftedMask;

                    int[] compressedVertices = GenerationConstants.VerticesByIteration[normalIndex];


                    _Vertexes[_VertexesCount + 0] = localPosition
                                                    + ((unaryTraversalShiftedMask & compressedVertices[3])
                                                       | ((((compressedVertices[3] >> traversalNormalShift) * traversals) << traversalNormalShift)
                                                          & traversalShiftedMask));
                    _Vertexes[_VertexesCount + 1] = compressedUv & (int.MaxValue << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2));


                    _Vertexes[_VertexesCount + 2] = localPosition
                                                    + ((unaryTraversalShiftedMask & compressedVertices[2])
                                                       | ((((compressedVertices[2] >> traversalNormalShift) * traversals) << traversalNormalShift)
                                                          & traversalShiftedMask));
                    _Vertexes[_VertexesCount + 3] = compressedUv & (int.MaxValue << GenerationConstants.CHUNK_SIZE_BIT_SHIFT);


                    _Vertexes[_VertexesCount + 4] = localPosition
                                                    + ((unaryTraversalShiftedMask & compressedVertices[1])
                                                       | ((((compressedVertices[1] >> traversalNormalShift) * traversals) << traversalNormalShift)
                                                          & traversalShiftedMask));
                    _Vertexes[_VertexesCount + 5] =
                        compressedUv & ~(GenerationConstants.CHUNK_SIZE_BIT_MASK << GenerationConstants.CHUNK_SIZE_BIT_SHIFT);


                    _Vertexes[_VertexesCount + 6] = localPosition
                                                    + ((unaryTraversalShiftedMask & compressedVertices[0])
                                                       | ((((compressedVertices[0] >> traversalNormalShift) * traversals) << traversalNormalShift)
                                                          & traversalShiftedMask));
                    _Vertexes[_VertexesCount + 7] = compressedUv & int.MaxValue;

                    _VertexesCount += 8;

                    break;
                }
            }
        }

        // private void NaiveMeshIndex(int index, int localPosition, ushort currentBlockId, bool isCurrentBlockTransparent)
        // {
        //     Debug.Assert(currentBlockId != BlockController.AirID, $"{nameof(TraverseIndex)} should not run on air blocks.");
        //     Debug.Assert((index >= 0) && (index < GenerationConstants.CHUNK_SIZE_CUBED), $"{nameof(index)} is not within chunk bounds.");
        //     Debug.Assert(WydMath.PointToIndex(DecompressVertex(localPosition), GenerationConstants.CHUNK_SIZE) == index,
        //         $"{nameof(localPosition)} does not match given {nameof(index)}.");
        //     Debug.Assert(_MeshingBlocks[index].ID == currentBlockId, $"{currentBlockId} is not equal to block ID at given index.");
        //     Debug.Assert(
        //         BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent) == isCurrentBlockTransparent,
        //         $"Given transparency state for {nameof(currentBlockId)} does not match actual block transparency.");
        //
        //     // iterate once over all 6 faces of given cubic space
        //     for (int normalIndex = 0; normalIndex < 6; normalIndex++)
        //     {
        //         // face direction always exists on a single bit, so shift 1 by the current normalIndex (0-5)
        //         Direction faceDirection = (Direction)(1 << normalIndex);
        //
        //         if (_MeshingBlocks[index].HasFace(faceDirection))
        //         {
        //             continue;
        //         }
        //
        //         // indicates whether the current face checking direction is negative or positive
        //         bool isNegativeFace = (normalIndex - 3) >= 0;
        //         // normalIndex constrained to represent the 3 axes
        //         int iModulo3 = normalIndex % 3;
        //         int iModulo3Shift = GenerationConstants.CHUNK_SIZE_BIT_SHIFT * iModulo3;
        //         // axis value of the current face check direction
        //         // example: for iteration normalIndex == 0—which is positive X—it'd be equal to localPosition.x
        //         int faceCheckAxisValue = (localPosition >> iModulo3Shift) & GenerationConstants.CHUNK_SIZE_BIT_MASK;
        //         // indicates whether or not the face check is within the current chunk bounds
        //         bool isFaceCheckOutOfBounds = (!isNegativeFace && (faceCheckAxisValue == (GenerationConstants.CHUNK_SIZE - 1)))
        //                                       || (isNegativeFace && (faceCheckAxisValue == 0));
        //
        //         if (!isFaceCheckOutOfBounds)
        //         {
        //             // amount by integer to add to current traversal index to get 3D->1D position of facing block
        //             int facedBlockIndex = index + GenerationConstants.IndexStepByNormalIndex[normalIndex];
        //             // if so, index into block ids and set facingBlockId
        //             ushort facedBlockId = _MeshingBlocks[facedBlockIndex].ID;
        //
        //             // if transparent, traverse so long as facing block is not the same block id
        //             // if opaque, traverse so long as facing block is transparent
        //             if (isCurrentBlockTransparent)
        //             {
        //                 if (currentBlockId == facedBlockId)
        //                 {
        //                     continue;
        //                 }
        //             }
        //             else if (!BlockController.Current.CheckBlockHasProperty(facedBlockId, BlockDefinition.Property.Transparent))
        //             {
        //                 Direction inverseFaceDirection = (Direction)(1 << ((normalIndex + 3) % 6));
        //                 _MeshingBlocks[facedBlockIndex].SetFace(inverseFaceDirection);
        //
        //                 continue;
        //             }
        //         }
        //         else
        //         {
        //             // this block of code translates the integer local position to the local position of the neighbor at [normalIndex]
        //             int sign = isNegativeFace ? -1 : 1;
        //             int iModuloComponentMask = GenerationConstants.CHUNK_SIZE_BIT_MASK << iModulo3Shift;
        //             int finalLocalPosition = (~iModuloComponentMask & localPosition)
        //                                      | (WydMath.Wrap(((localPosition & iModuloComponentMask) >> iModulo3Shift) + sign,
        //                                             GenerationConstants.CHUNK_SIZE, 0, GenerationConstants.CHUNK_SIZE - 1)
        //                                         << iModulo3Shift);
        //
        //             // index into neighbor blocks collections, call .GetPoint() with adjusted local position
        //             // remark: if there's no neighbor at the index given, then no chunk exists there (for instance,
        //             //     chunks at the edge of render distance). In this case, return NullID so no face is rendered on edges.
        //             ushort facedBlockId = _NeighborBlocksCollections[normalIndex]?.GetPoint(DecompressVertex(finalLocalPosition))
        //                                   ?? BlockController.NullID;
        //
        //             if (isCurrentBlockTransparent)
        //             {
        //                 if (currentBlockId == facedBlockId)
        //                 {
        //                     continue;
        //                 }
        //             }
        //             else if (!BlockController.Current.CheckBlockHasProperty(facedBlockId, BlockDefinition.Property.Transparent))
        //             {
        //                 continue;
        //             }
        //         }
        //
        //         if (!BlockController.Current.GetUVs(currentBlockId, faceDirection, out ushort textureId))
        //         {
        //             continue;
        //         }
        //
        //         int compressedUv = (textureId << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2))
        //                            ^ (1 << GenerationConstants.CHUNK_SIZE_BIT_SHIFT)
        //                            ^ 1;
        //
        //         int[] compressedVertices = GenerationConstants.VerticesByIteration[normalIndex];
        //
        //         _MeshingBlocks[index].SetFace(faceDirection);
        //
        //         int verticesCount = _MeshData.VerticesCount / 2;
        //         int transparentAsInt = Convert.ToInt32(isCurrentBlockTransparent);
        //
        //         _MeshData.AddTriangle(transparentAsInt, 0 + verticesCount);
        //         _MeshData.AddTriangle(transparentAsInt, 2 + verticesCount);
        //         _MeshData.AddTriangle(transparentAsInt, 1 + verticesCount);
        //         _MeshData.AddTriangle(transparentAsInt, 2 + verticesCount);
        //         _MeshData.AddTriangle(transparentAsInt, 3 + verticesCount);
        //         _MeshData.AddTriangle(transparentAsInt, 1 + verticesCount);
        //
        //         _MeshData.AddVertex(localPosition + compressedVertices[3]);
        //         _MeshData.AddVertex(compressedUv & (int.MaxValue << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2)));
        //
        //         _MeshData.AddVertex(localPosition + compressedVertices[2]);
        //         _MeshData.AddVertex(compressedUv & (int.MaxValue << GenerationConstants.CHUNK_SIZE_BIT_SHIFT));
        //
        //         _MeshData.AddVertex(localPosition + compressedVertices[1]);
        //         _MeshData.AddVertex(compressedUv & ~(GenerationConstants.CHUNK_SIZE_BIT_MASK << GenerationConstants.CHUNK_SIZE_BIT_SHIFT));
        //
        //         _MeshData.AddVertex(localPosition + compressedVertices[0]);
        //         _MeshData.AddVertex(compressedUv & int.MaxValue);
        //     }
        // }

        #endregion

        #region Vertex Compression

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompressVertex(int3 vertex) =>
            (vertex.x & GenerationConstants.CHUNK_SIZE_BIT_MASK)
            | ((vertex.y & GenerationConstants.CHUNK_SIZE_BIT_MASK) << GenerationConstants.CHUNK_SIZE_BIT_SHIFT)
            | ((vertex.z & GenerationConstants.CHUNK_SIZE_BIT_MASK) << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int3 DecompressVertex(int vertex) =>
            new int3(vertex & GenerationConstants.CHUNK_SIZE_BIT_MASK,
                (vertex >> GenerationConstants.CHUNK_SIZE_BIT_SHIFT) & GenerationConstants.CHUNK_SIZE_BIT_MASK,
                (vertex >> (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2)) & GenerationConstants.CHUNK_SIZE_BIT_MASK);

        #endregion
    }
}
