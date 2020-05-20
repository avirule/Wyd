#region

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Mathematics;
using Wyd.Collections;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Jobs;
using Wyd.World.Blocks;

#endregion

namespace Wyd.World.Chunks.Generation
{
    public static class ChunkMeshingJob
    {
        private const int _ARRAY_SIZE_IN_PACKED_VERTEXES_ANd_TEX_COORDS = 4 * 6 * GenerationConstants.CHUNK_SIZE_CUBED * 2;
        private const int _ARRAY_SIZE_IN_TRIANGLES = (int)(_ARRAY_SIZE_IN_PACKED_VERTEXES_ANd_TEX_COORDS * 1.5);

        private static readonly ArrayPool<MeshingBlock> _MeshingBlocksPool = ArrayPool<MeshingBlock>.Create(GenerationConstants.CHUNK_SIZE_CUBED, 8);

        private static readonly ArrayPool<int> _VertexesArrayPool =
            ArrayPool<int>.Create(_ARRAY_SIZE_IN_PACKED_VERTEXES_ANd_TEX_COORDS, ConcurrentWorkers.Count);

        private static readonly ArrayPool<int> _TrianglesArrayPool = ArrayPool<int>.Create(_ARRAY_SIZE_IN_TRIANGLES, ConcurrentWorkers.Count);
        private static readonly SemaphoreSlim _ResourceAccessSemaphore = new SemaphoreSlim(ConcurrentWorkers.Count);

        public static object ProcessMesh(int3 originPoint, INodeCollection<ushort> blocksCollection, bool advancedMeshing)
        {
            Debug.Assert(blocksCollection != null);

            if (blocksCollection.IsUniform && (blocksCollection.Value == BlockController.AirID))
            {
                return null;
            }

            _ResourceAccessSemaphore.Wait();

            MeshingBlock[] meshingBlocks = _MeshingBlocksPool.Rent(GenerationConstants.CHUNK_SIZE_CUBED);
            MeshData meshData = new MeshData(ReleaseResources, _VertexesArrayPool.Rent(_ARRAY_SIZE_IN_PACKED_VERTEXES_ANd_TEX_COORDS),
                _TrianglesArrayPool.Rent(_ARRAY_SIZE_IN_TRIANGLES));

            int index = 0;
            for (int y = 0; y < GenerationConstants.CHUNK_SIZE; y++)
            for (int z = 0; z < GenerationConstants.CHUNK_SIZE; z++)
            for (int x = 0; x < GenerationConstants.CHUNK_SIZE; x++, index++)
            {
                meshingBlocks[index].ID = ((Octree)blocksCollection).GetPoint(x, y, z);
            }

            INodeCollection<ushort>[] neighborBlocksCollections = new INodeCollection<ushort>[6];

            for (int normalIndex = 0; normalIndex < 6; normalIndex++)
            {
                int3 globalPosition = originPoint + (GenerationConstants.NormalVectorByIteration[normalIndex] * GenerationConstants.CHUNK_SIZE);

                if (WorldController.Current.TryGetChunk(globalPosition, out ChunkController chunkController))
                {
                    neighborBlocksCollections[normalIndex] = chunkController.Blocks;
                }
            }

            if (advancedMeshing)
            {
                GenerateTraversalMesh(neighborBlocksCollections, meshingBlocks, meshData);
            }
            else
            {
                //await BatchTasksAndAwaitAll().ConfigureAwait(false);
            }


            _MeshingBlocksPool.Return(meshingBlocks, true);

            return meshData;
        }

        private static void ReleaseResources(int[] vertexes, int[] triangles)
        {
            _VertexesArrayPool.Return(vertexes);
            _TrianglesArrayPool.Return(triangles);
            _ResourceAccessSemaphore.Release();
        }

        // protected void ProcessIndex(int index)
        // {
        //     // if (_CancellationToken.IsCancellationRequested)
        //     // {
        //     //     return;
        //     // }
        //
        //     ushort currentBlockId = _MeshingBlocks[index].ID;
        //
        //     if (currentBlockId == BlockController.AirID)
        //     {
        //         return;
        //     }
        //
        //     int localPosition = CompressVertex(WydMath.IndexTo3D(index, GenerationConstants.CHUNK_SIZE));
        //
        //     bool transparentTraversal = BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent);
        //
        //     NaiveMeshIndex(index, localPosition, currentBlockId, transparentTraversal);
        // }
        //
        // protected Task ProcessFinished()
        // {
        //     //if (!_CancellationToken.IsCancellationRequested)
        //     //{
        //     Singletons.Diagnostics.Instance["ChunkPreMeshing"].Enqueue(_PreMeshingTimeSpan);
        //     Singletons.Diagnostics.Instance["ChunkMeshing"].Enqueue(_MeshingTimeSpan);
        //     //}
        //
        //     return Task.CompletedTask;
        // }


        #region Mesh Generation

        /// <summary>
        ///     Generates the mesh data.
        /// </summary>
        /// <remarks>
        ///     The generated data is stored in the <see cref="MeshData" /> object <see cref="_MeshData" />.
        /// </remarks>
        private static void GenerateTraversalMesh(IReadOnlyList<INodeCollection<ushort>> neighborNodesCollection, MeshingBlock[] meshingBlocks,
            MeshData meshData)
        {
            Debug.Assert(meshingBlocks.Length == GenerationConstants.CHUNK_SIZE_CUBED, $"{meshingBlocks} should be the same length as chunk data.");

            int index = 0;
            for (int y = 0; y < GenerationConstants.CHUNK_SIZE; y++)
            for (int z = 0; z < GenerationConstants.CHUNK_SIZE; z++)
            for (int x = 0; x < GenerationConstants.CHUNK_SIZE; x++, index++)
            {
                // if (_CancellationToken.IsCancellationRequested)
                // {
                //     return;
                // }

                ushort currentBlockId = meshingBlocks[index].ID;

                if (currentBlockId == BlockController.AirID)
                {
                    continue;
                }

                int localPosition = x | (y << GenerationConstants.CHUNK_SIZE_BIT_SHIFT) | (z << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2));

                TraverseIndex(neighborNodesCollection, meshingBlocks, meshData, index, localPosition, currentBlockId,
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
        private static void TraverseIndex(IReadOnlyList<INodeCollection<ushort>> neighborBlocksCollection, MeshingBlock[] meshingBlocks,
            MeshData meshData, int index, int localPosition, ushort currentBlockId, bool isCurrentBlockTransparent)
        {
            Debug.Assert(currentBlockId != BlockController.AirID, $"{nameof(TraverseIndex)} should not run on air blocks.");
            Debug.Assert((index >= 0) && (index < GenerationConstants.CHUNK_SIZE_CUBED), $"{nameof(index)} is not within chunk bounds.");
            Debug.Assert(WydMath.PointToIndex(DecompressVertex(localPosition), GenerationConstants.CHUNK_SIZE) == index,
                $"{nameof(localPosition)} does not match given {nameof(index)}.");
            Debug.Assert(meshingBlocks[index].ID == currentBlockId, $"{currentBlockId} is not equal to block ID at given index.");
            Debug.Assert(
                BlockController.Current.CheckBlockHasProperty(currentBlockId, BlockDefinition.Property.Transparent) == isCurrentBlockTransparent,
                $"Given transparency state for {nameof(currentBlockId)} does not match actual block transparency.");

            // iterate once over all 6 faces of given cubic space
            for (int normalIndex = 0; normalIndex < 6; normalIndex++)
            {
                // face direction always exists on a single bit, so shift 1 by the current normalIndex (0-5)
                Direction faceDirection = (Direction)(1 << normalIndex);

                // check if current index has face already
                if (meshingBlocks[index].HasFace(faceDirection))
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
                        && !meshingBlocks[traversalIndex].HasFace(faceDirection)
                        && (meshingBlocks[traversalIndex].ID == currentBlockId);
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
                            ushort facedBlockId = meshingBlocks[facedBlockIndex].ID;

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
                                    meshingBlocks[facedBlockIndex].SetFace(inverseFaceDirection);
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
                            ushort facedBlockId = neighborBlocksCollection[normalIndex]?.GetPoint(DecompressVertex(finalLocalPosition))
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

                        meshingBlocks[traversalIndex].SetFace(faceDirection);
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

                    // the vertex index is vertex count / 2 since our layout is vertex->texcoord->vertex->texcoord->etc.
                    int trianglesVertexIndex = meshData.VertexesCount / 2;

                    meshData.Triangles[meshData.TrianglesCount + 0] = trianglesVertexIndex + 0;
                    meshData.Triangles[meshData.TrianglesCount + 1] = trianglesVertexIndex + 2;
                    meshData.Triangles[meshData.TrianglesCount + 2] = trianglesVertexIndex + 1;
                    meshData.Triangles[meshData.TrianglesCount + 3] = trianglesVertexIndex + 2;
                    meshData.Triangles[meshData.TrianglesCount + 4] = trianglesVertexIndex + 3;
                    meshData.Triangles[meshData.TrianglesCount + 5] = trianglesVertexIndex + 1;
                    meshData.TrianglesCount += 6;

                    int uvShift = (iModulo3 + traversalNormalIndex) % 2;
                    int compressedUv = (textureId << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2))
                                       | (1 << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * ((uvShift + 1) % 2)))
                                       | (traversals << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * uvShift));

                    int traversalShiftedMask = GenerationConstants.CHUNK_SIZE_BIT_MASK << traversalNormalShift;
                    int unaryTraversalShiftedMask = ~traversalShiftedMask;

                    int[] compressedVertices = GenerationConstants.VerticesByIteration[normalIndex];


                    meshData.Vertexes[meshData.VertexesCount + 0] = localPosition
                                                           + ((unaryTraversalShiftedMask & compressedVertices[3])
                                                              | ((((compressedVertices[3] >> traversalNormalShift) * traversals)
                                                                  << traversalNormalShift)
                                                                 & traversalShiftedMask));
                    meshData.Vertexes[meshData.VertexesCount + 1] = compressedUv & (int.MaxValue << (GenerationConstants.CHUNK_SIZE_BIT_SHIFT * 2));


                    meshData.Vertexes[meshData.VertexesCount + 2] = localPosition
                                                           + ((unaryTraversalShiftedMask & compressedVertices[2])
                                                              | ((((compressedVertices[2] >> traversalNormalShift) * traversals)
                                                                  << traversalNormalShift)
                                                                 & traversalShiftedMask));
                    meshData.Vertexes[meshData.VertexesCount + 3] = compressedUv & (int.MaxValue << GenerationConstants.CHUNK_SIZE_BIT_SHIFT);


                    meshData.Vertexes[meshData.VertexesCount + 4] = localPosition
                                                           + ((unaryTraversalShiftedMask & compressedVertices[1])
                                                              | ((((compressedVertices[1] >> traversalNormalShift) * traversals)
                                                                  << traversalNormalShift)
                                                                 & traversalShiftedMask));
                    meshData.Vertexes[meshData.VertexesCount + 5] =
                        compressedUv & ~(GenerationConstants.CHUNK_SIZE_BIT_MASK << GenerationConstants.CHUNK_SIZE_BIT_SHIFT);


                    meshData.Vertexes[meshData.VertexesCount + 6] = localPosition
                                                           + ((unaryTraversalShiftedMask & compressedVertices[0])
                                                              | ((((compressedVertices[0] >> traversalNormalShift) * traversals)
                                                                  << traversalNormalShift)
                                                                 & traversalShiftedMask));
                    meshData.Vertexes[meshData.VertexesCount + 7] = compressedUv & int.MaxValue;

                    meshData.VertexesCount += 8;

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
        //
        // private void FinishMeshing()
        // {
        //     // clear mask, add to object pool, and unset reference
        //     _MeshingBlocks = null;
        //
        //     // clear array to free RAM until next execution
        //     Array.Clear(_NeighborBlocksCollections, 0, _NeighborBlocksCollections.Length);
        //
        //     _OriginPoint = default;
        //     _BlocksCollection = default;
        //     _AggressiveFaceMerging = default;
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
