#region

using System;
using System.Threading;
using Serilog;
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

        private bool _GpuAcceleration;
        private float[] _NoiseMap;

        public TimeSpan NoiseRetrievalTimeSpan { get; private set; }
        public TimeSpan TerrainGenerationTimeSpan { get; private set; }

        public ChunkTerrainBuilder(CancellationToken cancellationToken, float3 originPoint, float frequency,
            float persistence, bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null)
            : base(cancellationToken, originPoint)
        {
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesBuffer = noiseValuesBuffer;
        }

        #region Terrain Generation

        private void GetComputeBufferData()
        {
            _NoiseValuesBuffer?.GetData(_NoiseMap);
            _NoiseValuesBuffer?.Release();
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
            _NoiseMap = _noiseValuesCache.Retrieve() ?? new float[ChunkController.SIZE_CUBED];

            if (_GpuAcceleration && (_NoiseValuesBuffer != null))
            {
                using (ManualResetEvent manualResetEvent = new ManualResetEvent(false))
                {
                    MainThreadActionsController.Current.PushAction(new MainThreadAction(manualResetEvent,
                        GetComputeBufferData));
                    manualResetEvent.WaitOne();
                }
            }
            else if (_GpuAcceleration && (_NoiseValuesBuffer == null))
            {
                Log.Warning(
                    $"`{nameof(_GpuAcceleration)}` is set to true, but no noise values were provided. Defaulting to CPU-bound generation.");
                _GpuAcceleration = false;
            }
            else
            {
                for (int index = 0; index < WydMath.Product(_Blocks.Volume.Size); index++)
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _NoiseMap[index] =
                        GetNoiseValueByGlobalPosition(_Blocks.Volume.MinPoint
                                                      + WydMath.IndexTo3D(index, ChunkController.Size3D));
                }
            }
        }

        private void Generate()
        {
            bool allAir = true;
            bool allStone = true;

            for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }
                else if (!allAir && !allStone)
                {
                    break;
                }

                if (_NoiseMap[index] >= 0.01f)
                {
                    allAir = false;
                }

                if (_NoiseMap[index] < 0.011f)
                {
                    allStone = false;
                }
            }

            float3 volumeCenterPoint = OriginPoint + ChunkController.Size3DExtents;

            if (allStone)
            {
                _Blocks = new OctreeNode<ushort>(volumeCenterPoint, ChunkController.SIZE, GetCachedBlockID("stone"));
                return;
            }
            else
            {
                _Blocks = new OctreeNode<ushort>(volumeCenterPoint, ChunkController.SIZE, BlockController.AirID);

                if (allAir)
                {
                    return;
                }
            }

            for (int index = ChunkController.SIZE_CUBED - 1; index >= 0; index--)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (_NoiseMap[index] < 0.01f)
                {
                    continue; // air
                }

                float3 localPosition = WydMath.IndexTo3D(index, ChunkController.Size3D);
                float3 globalPosition = OriginPoint + localPosition;

                if (_Blocks.UncheckedGetPoint(globalPosition) != BlockController.AirID)
                {
                    continue;
                }

                _Blocks.UncheckedSetPoint(globalPosition, GetBlockIDAtPosition(globalPosition));
            }
        }

        private ushort GetBlockIDAtPosition(float3 globalPosition)
        {
            if ((globalPosition.y < 4) && (globalPosition.y <= SeededRandom.Next(0, 4)))
            {
                return GetCachedBlockID("bedrock");
            }
            else
            {
                return GetCachedBlockID("stone");
            }
        }

        private float GetNoiseValueByGlobalPosition(float3 globalPosition)
        {
            float noiseValue = OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, _Frequency, globalPosition);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, WorldController.WORLD_HEIGHT, globalPosition.y));
            noiseValue /= globalPosition.y + (-1f * _Persistence);

            return noiseValue;
        }

        #endregion
    }
}
