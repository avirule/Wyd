#region

using System;
using System.Diagnostics;
using System.Threading;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.System;
using Wyd.System.Jobs;
using Wyd.System.Noise;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkRawTerrainBuilder : ChunkBuilder
    {
        private readonly object _NoiseValuesReadyHandle = new object();
        private readonly Stopwatch _Stopwatch;
        private readonly float _Frequency;
        private readonly float _Persistence;
        private readonly ComputeBuffer _NoiseValuesBuffer;

        private bool _NoiseValuesReady;
        private bool _GpuAcceleration;
        private ChunkBuilderNoiseValues _NoiseValues;

        public TimeSpan NoiseRetrievalTimeSpan { get; private set; }
        public TimeSpan TerrainGenerationTimeSpan { get; private set; }

        private bool NoiseValuesReady
        {
            get
            {
                bool tmp;

                lock (_NoiseValuesReadyHandle)
                {
                    tmp = _NoiseValuesReady;
                }

                return tmp;
            }
            set
            {
                lock (_NoiseValuesReadyHandle)
                {
                    _NoiseValuesReady = value;
                }
            }
        }

        public ChunkRawTerrainBuilder(GenerationData generationData, float frequency, float persistence,
            bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null)
        {
            SetGenerationData(generationData);
            _Stopwatch = new Stopwatch();
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesBuffer = noiseValuesBuffer;
            NoiseValuesReady = false;
        }

        private void GetComputeBufferData()
        {
            _NoiseValuesBuffer.GetData(_NoiseValues);
            _NoiseValuesBuffer.Release();
            NoiseValuesReady = true;
        }

        public void Generate()
        {
            try
            {
                if (_GenerationData.Blocks == default)
                {
                    Log.Error($"`{nameof(_GenerationData.Blocks)}` has not been set. Aborting generation.");
                    return;
                }

                GenerateNoise();

                _Stopwatch.Restart();
                _GenerationData.Blocks.Collapse(true);

                for (int index = WydMath.Product(ChunkController.Size) - 1; index >= 0; index--)
                {
                    float3 globalPosition =
                        _GenerationData.Bounds.MinPoint + WydMath.IndexTo3D(index, ChunkController.Size);
                    _GenerationData.Blocks.SetPoint(globalPosition, GetBlockIDAtPosition(globalPosition, index));
                }

                NoiseValuesCache.CacheItem(ref _NoiseValues);

                _Stopwatch.Stop();
                TerrainGenerationTimeSpan = _Stopwatch.Elapsed;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void GenerateNoise()
        {
            _Stopwatch.Restart();

            _NoiseValues = NoiseValuesCache.Retrieve() ?? new ChunkBuilderNoiseValues();

            if (_GpuAcceleration && (_NoiseValuesBuffer != null))
            {
                ManualResetEvent manualResetEvent = new ManualResetEvent(false);
                MainThreadActionsController.Current.PushAction(new MainThreadAction(manualResetEvent,
                    GetComputeBufferData));
                manualResetEvent.WaitOne();
            }
            else if (_GpuAcceleration && (_NoiseValuesBuffer == null))
            {
                Log.Warning(
                    $"`{nameof(_GpuAcceleration)}` is set to true, but no noise values were provided. Defaulting to CPU-bound generation.");
                _GpuAcceleration = false;
            }
            else
            {
                for (int index = 0; index < WydMath.Product(_GenerationData.Bounds.Size); index++)
                {
                    _NoiseValues[index] =
                        GetNoiseValueByGlobalPosition(_GenerationData.Bounds.MinPoint
                                                      + WydMath.IndexTo3D(index, ChunkController.Size));
                }
            }

            _Stopwatch.Stop();
            NoiseRetrievalTimeSpan = _Stopwatch.Elapsed;
        }

        private ushort GetBlockIDAtPosition(float3 globalPosition, int index)
        {
            return _NoiseValues[index] <= 0.01f ? (ushort)1 : (ushort)0;

            if ((globalPosition.y < 4) && (globalPosition.y <= _Rand.Next(0, 4)))
            {
                return GetCachedBlockID("bedrock");
            }

            if (_NoiseValues[index] < 0.01f)
            {
                return BlockController.AIR_ID;
            }

            if (globalPosition.y > 135)
            {
                return GetCachedBlockID("grass");
            }
            else
            {
                return GetCachedBlockID("stone");
            }
        }

        protected float GetNoiseValueByGlobalPosition(float3 globalPosition)
        {
            float noiseValue = OpenSimplex_FastNoise.GetSimplex(WorldController.Current.Seed, _Frequency,
                globalPosition.x, globalPosition.y, globalPosition.z);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, WorldController.WORLD_HEIGHT, globalPosition.y));
            noiseValue /= globalPosition.y + (-1f * _Persistence);

            return noiseValue;
        }
    }
}
