#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Collections;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World;
using Wyd.Noise;
using Wyd.Singletons;

#endregion

namespace Wyd.World.Chunks.Generation
{
    public class ChunkTerrainBuilderJob : ChunkTerrainJob
    {
        private static readonly ObjectPool<int[]> _HeightmapPool = new ObjectPool<int[]>();
        private static readonly ObjectPool<float[]> _CaveNoisePool = new ObjectPool<float[]>();

        private readonly int _NoiseSeedA;
        private readonly int _NoiseSeedB;

        private float _Frequency;
        private float _Persistence;
        private TimeSpan _NoiseRetrievalTimeSpan;
        private TimeSpan _TerrainGenerationTimeSpan;

        private ComputeBuffer _HeightmapBuffer;
        private ComputeBuffer _CaveNoiseBuffer;
        private int[] _Heightmap;
        private float[] _CaveNoise;

        public ChunkTerrainBuilderJob()
        {
            int seed = WorldController.Current.Seed;

            _NoiseSeedA = seed ^ (seed * 2);
            _NoiseSeedB = seed ^ (seed * 3);
        }

        protected override async Task Process()
        {
            Stopwatch.Restart();

            await GenerateNoise().ConfigureAwait(false);

            Stopwatch.Stop();

            _NoiseRetrievalTimeSpan = Stopwatch.Elapsed;

            Stopwatch.Restart();

            _Blocks = new Octree(GenerationConstants.CHUNK_SIZE, BlockController.AirID, false);

            await BatchTasksAndAwaitAll().ConfigureAwait(false);

            Array.Clear(_Heightmap, 0, _Heightmap.Length);
            Array.Clear(_CaveNoise, 0, _CaveNoise.Length);

            _HeightmapPool.TryAdd(_Heightmap);
            _CaveNoisePool.TryAdd(_CaveNoise);

            _Heightmap = null;
            _CaveNoise = null;

            Stopwatch.Stop();

            _TerrainGenerationTimeSpan = Stopwatch.Elapsed;
        }

        protected override Task ProcessIndex(int index)
        {
            GenerateIndex(index);

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!_CancellationToken.IsCancellationRequested)
            {
                Singletons.Diagnostics.Instance["ChunkNoiseRetrieval"].Enqueue(_NoiseRetrievalTimeSpan);
                Singletons.Diagnostics.Instance["ChunkBuilding"].Enqueue(_TerrainGenerationTimeSpan);
            }

            return Task.CompletedTask;
        }

        public void SetData(CancellationToken cancellationToken, int3 originPoint, float frequency, float persistence,
            ComputeBuffer heightmapBuffer = null, ComputeBuffer caveNoiseBuffer = null)
        {
            SetData(cancellationToken, originPoint);

            _Frequency = frequency;
            _Persistence = persistence;
            _HeightmapBuffer = heightmapBuffer;
            _CaveNoiseBuffer = caveNoiseBuffer;
        }

        public void ClearData()
        {
            _OriginPoint = default;
            _Frequency = default;
            _Persistence = default;
            _Blocks = default;
        }

        private bool GetComputeBufferData()
        {
            _HeightmapBuffer?.GetData(_Heightmap);
            _CaveNoiseBuffer?.GetData(_CaveNoise);

            _HeightmapBuffer?.Release();
            _CaveNoiseBuffer?.Release();

            return true;
        }

        private async Task GenerateNoise()
        {
            // SIZE_SQUARED + 1 to facilitate compute shader's above-y-value count
            _Heightmap = _HeightmapPool.Retrieve() ?? new int[GenerationConstants.CHUNK_SIZE_SQUARED];
            _CaveNoise = _CaveNoisePool.Retrieve() ?? new float[GenerationConstants.CHUNK_SIZE_CUBED];

            if ((_HeightmapBuffer == null) || (_CaveNoiseBuffer == null))
            {
                for (int x = 0; x < GenerationConstants.CHUNK_SIZE; x++)
                for (int z = 0; z < GenerationConstants.CHUNK_SIZE; z++)
                {
                    int2 xzCoords = new int2(x, z);
                    int heightmapIndex = WydMath.PointToIndex(xzCoords, GenerationConstants.CHUNK_SIZE);
                    _Heightmap[heightmapIndex] = GetHeightByGlobalPosition(_OriginPoint.xz + xzCoords);

                    for (int y = 0; y < GenerationConstants.CHUNK_SIZE; y++)
                    {
                        int3 localPosition = new int3(x, y, z);
                        int3 globalPosition = _OriginPoint + localPosition;
                        int caveNoiseIndex = WydMath.PointToIndex(localPosition, GenerationConstants.CHUNK_SIZE);

                        _CaveNoise[caveNoiseIndex] = GetCaveNoiseByGlobalPosition(globalPosition);
                    }
                }
            }
            else
            {
                // ReSharper disable once ConvertToUsingDeclaration
                // .... because Rider doesn't actually consider language feature version
                // remark: thanks JetBrains
                using (SemaphoreSlim semaphoreReset = MainThreadActionsController.Current.QueueAction(GetComputeBufferData))
                {
                    await semaphoreReset.WaitAsync(_CancellationToken);
                }
            }
        }

        private void GenerateIndex(int index)
        {
            int3 localPosition = WydMath.IndexTo3D(index, GenerationConstants.CHUNK_SIZE);
            int heightmapIndex = WydMath.PointToIndex(localPosition.xz, GenerationConstants.CHUNK_SIZE);

            int noiseHeight = _Heightmap[heightmapIndex];

            if (noiseHeight < _OriginPoint.y)
            {
                return;
            }

            int globalPositionY = _OriginPoint.y + localPosition.y;

            if ((globalPositionY < 4) && (globalPositionY <= _SeededRandom.Next(0, 4)))
            {
                _Blocks.SetPoint(localPosition, GetCachedBlockID("bedrock"));
                return;
            }
            else if (_CaveNoise[index] < 0.000225f)
            {
                return;
            }

            if (globalPositionY == noiseHeight)
            {
                _Blocks.SetPoint(localPosition, GetCachedBlockID("grass"));
            }
            else if ((globalPositionY < noiseHeight) && (globalPositionY >= (noiseHeight - 3))) // lay dirt up to 3 blocks below noise height
            {
                _Blocks.SetPoint(localPosition, _SeededRandom.Next(0, 8) == 0
                    ? GetCachedBlockID("dirt_coarse")
                    : GetCachedBlockID("dirt"));
            }
            else if (globalPositionY < (noiseHeight - 3))
            {
                if (_SeededRandom.Next(0, 100) == 0)
                {
                    _Blocks.SetPoint(localPosition, GetCachedBlockID("coal_ore"));
                }
                else
                {
                    _Blocks.SetPoint(localPosition, GetCachedBlockID("stone"));
                }
            }
        }

        private float GetCaveNoiseByGlobalPosition(int3 globalPosition)
        {
            float currentHeight = (globalPosition.y + (((WorldController.WORLD_HEIGHT / 4f) - (globalPosition.y * 1.25f)) * _Persistence)) * 0.85f;
            float heightDampener = math.unlerp(0f, WorldController.WORLD_HEIGHT, currentHeight);
            float noiseA = OpenSimplexSlim.GetSimplex(_NoiseSeedA, 0.01f, globalPosition) * heightDampener;
            float noiseB = OpenSimplexSlim.GetSimplex(_NoiseSeedB, 0.01f, globalPosition) * heightDampener;
            float noiseAPow2 = math.pow(noiseA, 2f);
            float noiseBPow2 = math.pow(noiseB, 2f);

            return (noiseAPow2 + noiseBPow2) / 2f;
        }

        private int GetHeightByGlobalPosition(int2 globalPosition)
        {
            float noise = OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, _Frequency, globalPosition);
            float noiseAsWorldHeight = math.unlerp(-1f, 1f, noise) * WorldController.WORLD_HEIGHT;
            float noisePersistedWorldHeight =
                noiseAsWorldHeight + (((WorldController.WORLD_HEIGHT / 2f) - (noiseAsWorldHeight * 1.25f)) * _Persistence);

            return (int)math.floor(noisePersistedWorldHeight);
        }
    }
}
