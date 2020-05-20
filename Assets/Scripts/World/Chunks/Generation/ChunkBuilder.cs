#region

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ConcurrentAsyncScheduler;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Collections;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Noise;
using Wyd.Singletons;
using Random = System.Random;

#endregion

namespace Wyd.World.Chunks.Generation
{
    public static class ChunkBuilder
    {
        private static readonly ConcurrentDictionary<string, ushort> _BlockIDCache = new ConcurrentDictionary<string, ushort>();

        private static readonly ArrayPool<int> _HeightmapPool =
            ArrayPool<int>.Create(GenerationConstants.CHUNK_SIZE_SQUARED, AsyncJobScheduler.MaximumConcurrentJobs);

        private static readonly ArrayPool<float> _CavemapPool =
            ArrayPool<float>.Create(GenerationConstants.CHUNK_SIZE_CUBED, AsyncJobScheduler.MaximumConcurrentJobs);

        private static readonly int _NoiseSeedA;
        private static readonly int _NoiseSeedB;

        static ChunkBuilder()
        {
            int seed = WorldController.Current.Seed;

            _NoiseSeedA = seed ^ 2;
            _NoiseSeedB = seed ^ 3;
        }

        public static object Generate(int3 originPoint, ComputeBuffer heightmapBuffer, ComputeBuffer cavemapBuffer, float frequency,
            float persistence)
        {
            // the chunk generator doesn't need to observe the resource semaphore because its memory-exhaustive operations are self-contained
            //    (that is, unlike the ChunkMesher, there is no data that needs to be released after execution finishes, as in the case of MeshData).

            (int[] heightmap, float[] cavemap) = GenerateNoise(originPoint, heightmapBuffer, cavemapBuffer, frequency, persistence);

            INodeCollection<ushort> blocks = new Octree(GenerationConstants.CHUNK_SIZE, BlockController.AirID, false);
            Random seededRandom = new Random(originPoint.GetHashCode());

            int index = 0;
            for (int y = 0; y < GenerationConstants.CHUNK_SIZE; y++)
            for (int z = 0; z < GenerationConstants.CHUNK_SIZE; z++)
            for (int x = 0; x < GenerationConstants.CHUNK_SIZE; x++, index++)
            {
                GenerateIndex(originPoint, index, new int3(x, y, z), heightmap, cavemap, seededRandom, blocks);
            }

            _HeightmapPool.Return(heightmap);
            _CavemapPool.Return(cavemap);

            return blocks;
        }

        private static bool GetComputeBufferData(ComputeBuffer heightmapBuffer, ComputeBuffer cavemapBuffer, int[] heightmap, float[] cavemap)
        {
            heightmapBuffer?.GetData(heightmap);
            cavemapBuffer?.GetData(cavemap);

            heightmapBuffer?.Release();
            cavemapBuffer?.Release();

            return true;
        }

        private static (int[] heightmap, float[] cavemap) GenerateNoise(int3 originPoint, ComputeBuffer heightmapBuffer, ComputeBuffer cavemapBuffer,
            float frequency, float persistence)
        {
            int[] heightmap = _HeightmapPool.Rent(GenerationConstants.CHUNK_SIZE_SQUARED);
            float[] cavemap = _CavemapPool.Rent(GenerationConstants.CHUNK_SIZE_CUBED);

            if ((heightmapBuffer == null) || (cavemapBuffer == null))
            {
                for (int x = 0; x < GenerationConstants.CHUNK_SIZE; x++)
                for (int z = 0; z < GenerationConstants.CHUNK_SIZE; z++)
                {
                    int2 xzCoords = new int2(x, z);
                    int heightmapIndex = WydMath.PointToIndex(xzCoords, GenerationConstants.CHUNK_SIZE);
                    heightmap[heightmapIndex] = GetHeightByGlobalPosition(originPoint.xz + xzCoords, frequency, persistence);

                    for (int y = 0; y < GenerationConstants.CHUNK_SIZE; y++)
                    {
                        int3 localPosition = new int3(x, y, z);
                        int3 globalPosition = originPoint + localPosition;
                        int caveNoiseIndex = WydMath.PointToIndex(localPosition, GenerationConstants.CHUNK_SIZE);

                        cavemap[caveNoiseIndex] = GetCaveNoiseByGlobalPosition(globalPosition, persistence);
                    }
                }
            }
            else
            {
                // ReSharper disable once ConvertToUsingDeclaration
                // .... because Rider doesn't actually consider language feature version
                // remark: thanks JetBrains
                using (ManualResetEventSlim manualReset = MainThreadActions.Instance.QueueAction(() => GetComputeBufferData(heightmapBuffer,
                    cavemapBuffer, heightmap, cavemap)))
                {
                    manualReset.Wait();
                }
            }

            return (heightmap, cavemap);
        }

        private static void GenerateIndex(int3 originPoint, int index, int3 localPosition, IReadOnlyList<int> heightmap, IReadOnlyList<float> cavemap,
            Random seededRandom, INodeCollection<ushort> blocks)
        {
            int globalPositionY = originPoint.y + localPosition.y;

            if ((globalPositionY < 4) && (globalPositionY <= seededRandom.Next(0, 4)))
            {
                blocks.SetPoint(localPosition, GetCachedBlockID("bedrock"));
                return;
            }
            else if (cavemap[index] < 0.000225f)
            {
                return;
            }

            int heightmapIndex = WydMath.PointToIndex(localPosition.xz, GenerationConstants.CHUNK_SIZE);
            int noiseHeight = heightmap[heightmapIndex];

            if (globalPositionY == noiseHeight)
            {
                blocks.SetPoint(localPosition, GetCachedBlockID("grass"));
            }
            else if ((globalPositionY < noiseHeight) && (globalPositionY >= (noiseHeight - 3))) // lay dirt up to 3 blocks below noise height
            {
                blocks.SetPoint(localPosition, seededRandom.Next(0, 8) == 0
                    ? GetCachedBlockID("dirt_coarse")
                    : GetCachedBlockID("dirt"));
            }
            else if (globalPositionY < (noiseHeight - 3))
            {
                blocks.SetPoint(localPosition, seededRandom.Next(0, 100) == 0
                    ? GetCachedBlockID("coal_ore")
                    : GetCachedBlockID("stone"));
            }
        }

        private static float GetCaveNoiseByGlobalPosition(int3 globalPosition, float persistence)
        {
            float currentHeight = (globalPosition.y + (((WorldController.WORLD_HEIGHT / 4f) - (globalPosition.y * 1.25f)) * persistence)) * 0.85f;
            float heightDampener = math.unlerp(0f, WorldController.WORLD_HEIGHT, currentHeight);
            float noiseA = OpenSimplexSlim.GetSimplex(_NoiseSeedA, 0.01f, globalPosition) * heightDampener;
            float noiseB = OpenSimplexSlim.GetSimplex(_NoiseSeedB, 0.01f, globalPosition) * heightDampener;
            float noiseAPow2 = math.pow(noiseA, 2f);
            float noiseBPow2 = math.pow(noiseB, 2f);

            return (noiseAPow2 + noiseBPow2) / 2f;
        }

        private static int GetHeightByGlobalPosition(int2 globalPosition, float frequency, float persistence)
        {
            float noise = OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, frequency, globalPosition);
            float noiseAsWorldHeight = math.unlerp(-1f, 1f, noise) * WorldController.WORLD_HEIGHT;
            float noisePersistedWorldHeight =
                noiseAsWorldHeight + (((WorldController.WORLD_HEIGHT / 2f) - (noiseAsWorldHeight * 1.25f)) * persistence);

            return (int)math.floor(noisePersistedWorldHeight);
        }

        private static ushort GetCachedBlockID(string blockName)
        {
            if (_BlockIDCache.TryGetValue(blockName, out ushort id))
            {
                return id;
            }
            else if (BlockController.Current.TryGetBlockId(blockName, out id))
            {
                _BlockIDCache.TryAdd(blockName, id);
                return id;
            }

            throw new ArgumentException("Block with given name does not exist.", nameof(blockName));
        }
    }
}
