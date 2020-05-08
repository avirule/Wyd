#region

using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public class ChunkTerrainBuilderJob : ChunkTerrainJob
    {
        private static readonly ObjectPool<int[]> _heightmapPool = new ObjectPool<int[]>();
        private static readonly ObjectPool<float[]> _caveNoisePool = new ObjectPool<float[]>();

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
            _NoiseSeedA = WorldController.Current.Seed ^ 2;
            _NoiseSeedB = WorldController.Current.Seed ^ 3;
        }

        protected override Task Process()
        {
            TimeMeasuredGenerate();

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!CancellationToken.IsCancellationRequested)
            {
                DiagnosticsController.Current.RollingNoiseRetrievalTimes.Enqueue(_NoiseRetrievalTimeSpan);
                DiagnosticsController.Current.RollingTerrainBuildingTimes.Enqueue(_TerrainGenerationTimeSpan);
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
            CancellationToken = default;
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

        private void TimeMeasuredGenerate()
        {
            Stopwatch.Restart();

            GenerateNoise();

            Stopwatch.Stop();

            _NoiseRetrievalTimeSpan = Stopwatch.Elapsed;

            Stopwatch.Restart();

            Generate();

            Array.Clear(_Heightmap, 0, _Heightmap.Length);
            Array.Clear(_CaveNoise, 0, _CaveNoise.Length);

            _heightmapPool.TryAdd(_Heightmap);
            _caveNoisePool.TryAdd(_CaveNoise);

            _Heightmap = null;
            _CaveNoise = null;

            Stopwatch.Stop();

            _TerrainGenerationTimeSpan = Stopwatch.Elapsed;
        }

        private void GenerateNoise()
        {
            // SIZE_SQUARED + 1 to facilitate compute shader's above-y-value count
            _Heightmap = _heightmapPool.Retrieve() ?? new int[ChunkController.SIZE_SQUARED];
            _CaveNoise = _caveNoisePool.Retrieve() ?? new float[ChunkController.SIZE_CUBED];

            if ((_HeightmapBuffer == null) || (_CaveNoiseBuffer == null))
            {
                for (int x = 0; x < ChunkController.SIZE; x++)
                for (int z = 0; z < ChunkController.SIZE; z++)
                {
                    int2 xzCoords = new int2(x, z);
                    int heightmapIndex = WydMath.PointToIndex(xzCoords, ChunkController.SIZE);
                    _Heightmap[heightmapIndex] = GetHeightByGlobalPosition(_OriginPoint.xz + xzCoords);

                    for (int y = 0; y < ChunkController.SIZE; y++)
                    {
                        int3 localPosition = new int3(x, y, z);
                        int3 globalPosition = _OriginPoint + localPosition;
                        int caveNoiseIndex = WydMath.PointToIndex(localPosition, ChunkController.SIZE);

                        _CaveNoise[caveNoiseIndex] = GetCaveNoiseByGlobalPosition(globalPosition);
                    }
                }
            }
            else
            {
                // ReSharper disable once ConvertToUsingDeclaration
                // .... because Rider doesn't actually consider language feature version
                // remark: thanks JetBrains
                using (ManualResetEvent manualResetEvent = MainThreadActionsController.Current.QueueAction(GetComputeBufferData))
                {
                    manualResetEvent.WaitOne();
                }
            }
        }

        private void Generate()
        {
            _Blocks = new Octree(ChunkController.SIZE, BlockController.AirID);

            for (int x = 0; x < ChunkController.SIZE; x++)
            for (int z = 0; z < ChunkController.SIZE; z++)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                int heightmapIndex = WydMath.PointToIndex(new int2(x, z), ChunkController.SIZE);
                int noiseHeight = _Heightmap[heightmapIndex];

                if (noiseHeight < _OriginPoint.y)
                {
                    continue;
                }

                int noiseHeightClamped = math.clamp(noiseHeight - _OriginPoint.y, 0, ChunkController.SIZE_MINUS_ONE);

                for (int y = noiseHeightClamped; y >= 0; y--)
                {
                    int3 localPosition = new int3(x, y, z);
                    int globalPositionY = _OriginPoint.y + y;

                    if ((globalPositionY < 4) && (globalPositionY <= _SeededRandom.Next(0, 4)))
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("bedrock"));
                        continue;
                    }
                    else if (_CaveNoise[WydMath.PointToIndex(localPosition, ChunkController.SIZE)] < 0.000225f)
                    {
                        continue;
                    }

                    if (globalPositionY == noiseHeight)
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("grass"));
                    }
                    // lay dirt up to 3 blocks below noise height
                    else if ((globalPositionY < noiseHeight) && (globalPositionY >= (noiseHeight - 3)))
                    {
                        _Blocks.SetPoint(localPosition, _SeededRandom.Next(0, 8) == 0
                            ? GetCachedBlockID("dirt_coarse")
                            : GetCachedBlockID("dirt"));
                    }
                    else if (_SeededRandom.Next(0, 100) == 0)
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("coal_ore"));
                    }
                    else
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("stone"));
                    }
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
