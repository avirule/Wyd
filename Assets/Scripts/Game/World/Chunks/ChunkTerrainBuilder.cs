#region

using System;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkTerrainBuilder : ChunkBuilder
    {
        private static readonly ObjectCache<float[]> _noiseValuesCache = new ObjectCache<float[]>();

        private readonly ComputeBuffer _NoiseValuesBuffer;
        private readonly float _Frequency;
        private readonly float _Persistence;

        private float[] _NoiseMap;

        public TimeSpan NoiseRetrievalTimeSpan { get; private set; }
        public TimeSpan TerrainGenerationTimeSpan { get; private set; }

        public ChunkTerrainBuilder(CancellationToken cancellationToken, int3 originPoint, float frequency, float persistence,
            ComputeBuffer noiseValuesBuffer) : base(cancellationToken, originPoint)
        {
            _Frequency = frequency;
            _Persistence = persistence;
            _NoiseValuesBuffer = noiseValuesBuffer;
        }

        #region Terrain Generation

        private bool GetComputeBufferData()
        {
            _NoiseValuesBuffer?.GetData(_NoiseMap);
            _NoiseValuesBuffer?.Release();

            return true;
        }

        public void TimeMeasuredGenerate()
        {
            Stopwatch.Restart();

            GenerateNoise();

            Stopwatch.Stop();

            NoiseRetrievalTimeSpan = Stopwatch.Elapsed;

            Stopwatch.Restart();

            Generate();

            _noiseValuesCache.CacheItem(ref _NoiseMap);

            Stopwatch.Stop();

            TerrainGenerationTimeSpan = Stopwatch.Elapsed;
        }

        private void GenerateNoise()
        {
            _NoiseMap = _noiseValuesCache.Retrieve() ?? new float[ChunkController.SIZE_SQUARED];

            if (_NoiseValuesBuffer == null)
            {
                for (int index = 0; index < _NoiseMap.Length; index++)
                {
                    int2 xzCoords = WydMath.IndexTo2D(index, ChunkController.SIZE);

                    _NoiseMap[index] = GetNoiseValueByGlobalPosition(xzCoords);
                }
            }
            else
            {
                using (ManualResetEvent manualResetEvent = new ManualResetEvent(false))
                {
                    MainThreadActionsController.Current.QueueAction(new MainThreadAction(manualResetEvent, GetComputeBufferData));
                    manualResetEvent.WaitOne();
                }
            }
        }

        private void Generate()
        {
            _Blocks = new OctreeNode<ushort>(ChunkController.SIZE, BlockController.AirID);

            for (int x = 0; x < ChunkController.SIZE; x++)
            for (int z = 0; z < ChunkController.SIZE; z++)
            {
                int2 xzPosition = new int2(x, z);
                int index = WydMath.PointToIndex(xzPosition, ChunkController.SIZE);
                int noiseHeight = (int)_NoiseMap[index];

                if (noiseHeight < 0)
                {
                    continue;
                }

                int absoluteNoiseHeight = noiseHeight + OriginPoint.y;
                bool placeSurfaceBlocks = true; // OpenSimplexSlim.GetSimplex(OriginPoint.GetHashCode(), 0.01f, xzPosition) <= 0.5f;

                for (int y = noiseHeight; y >= 0; y--)
                {
                    int3 localPosition = new int3(x, y, z);
                    int3 globalPosition = OriginPoint + localPosition;

                    if ((globalPosition.y < 4) && (globalPosition.y <= SeededRandom.Next(0, 4)))
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("bedrock"));
                    }
                    else if (globalPosition.y == absoluteNoiseHeight)
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("grass"));
                    }
                    else if ((globalPosition.y < absoluteNoiseHeight) && (globalPosition.y >= (absoluteNoiseHeight - 3)))
                    {
                        _Blocks.SetPoint(localPosition, SeededRandom.Next(0, 8) == 0
                            ? GetCachedBlockID("dirt_coarse")
                            : GetCachedBlockID("dirt"));
                    }
                    else if (SeededRandom.Next(0, 100) == 0)
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("coal_ore"));
                    }
                    else
                    {
                        _Blocks.SetPoint(localPosition, GetCachedBlockID("stone"));
                    }
                }
            }


            // bool allAir = true;
            // bool allStone = true;
            //
            // for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            // {
            //     if (CancellationToken.IsCancellationRequested)
            //     {
            //         return;
            //     }
            //     else if (!allAir && !allStone)
            //     {
            //         break;
            //     }
            //
            //     if (_NoiseMap[index] >= 0.01f)
            //     {
            //         allAir = false;
            //     }
            //
            //     if (_NoiseMap[index] < 0.01f)
            //     {
            //         allStone = false;
            //     }
            // }
            //
            // if (allStone)
            // {
            //     _Blocks = new OctreeNode<ushort>(ChunkController.SIZE, GetCachedBlockID("stone"));
            //     return;
            // }
            //
            // _Blocks = new OctreeNode<ushort>(ChunkController.SIZE, BlockController.AirID);
            //
            // if (allAir)
            // {
            //     return;
            // }
            //
            // for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            // {
            //     if (CancellationToken.IsCancellationRequested)
            //     {
            //         return;
            //     }
            //
            //     if (_NoiseMap[index] < 0.01f)
            //     {
            //         continue; // air
            //     }
            //
            //     int3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);
            //     int3 globalPosition = OriginPoint + localPosition;
            //
            //     if ((globalPosition.y < 4) && (globalPosition.y <= SeededRandom.Next(0, 4)))
            //     {
            //         _Blocks.SetPoint(localPosition, GetCachedBlockID("bedrock"));
            //     }
            //     else if (SeededRandom.Next(0, 100) == 0)
            //     {
            //         _Blocks.SetPoint(localPosition, GetCachedBlockID("coal_ore"));
            //     }
            //     else
            //     {
            //         _Blocks.SetPoint(localPosition, GetCachedBlockID("stone"));
            //     }
            // }
        }

        private float GetNoiseValueByGlobalPosition(int2 globalPosition)
        {
            float noise = OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, _Frequency, globalPosition);
            float interpolatedNoise = math.unlerp(-1f, 1f, noise);
            float noiseHeight = (interpolatedNoise * WorldController.WORLD_HEIGHT) - OriginPoint.y;

            return math.abs(noiseHeight);
        }

        #endregion
    }
}
