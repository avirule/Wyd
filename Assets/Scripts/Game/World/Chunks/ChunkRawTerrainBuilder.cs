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
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkRawTerrainBuilder : ChunkBuilder
    {
        private static readonly ObjectCache<float[]> _NoiseValuesCache = new ObjectCache<float[]>();

        private readonly Stopwatch _Stopwatch;
        private readonly ComputeBuffer _NoiseValuesBuffer;
        private readonly float _Frequency;
        private readonly float _Persistence;

        private bool _GpuAcceleration;
        private float[] _NoiseMap;

        public TimeSpan NoiseRetrievalTimeSpan { get; private set; }
        public TimeSpan TerrainGenerationTimeSpan { get; private set; }

        public ChunkRawTerrainBuilder(CancellationToken cancellationToken, float3 originPoint,
            ref OctreeNode<ushort> blocks, float frequency, float persistence, bool gpuAcceleration = false,
            ComputeBuffer noiseValuesBuffer = null) : base(cancellationToken, originPoint, ref blocks)
        {
            _Stopwatch = new Stopwatch();
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;
            _NoiseValuesBuffer = noiseValuesBuffer;
        }

        private void GetComputeBufferData()
        {
            _Stopwatch.Restart();

            _NoiseValuesBuffer?.GetData(_NoiseMap);
            _NoiseValuesBuffer?.Release();

            _Stopwatch.Stop();
        }

        public void Generate()
        {
            if (Blocks == default)
            {
                Log.Error($"`{nameof(Blocks)}` has not been set. Aborting generation.");
                return;
            }

            GenerateNoise();

            _Stopwatch.Restart();

            for (int index = WydMath.Product(ChunkController.Size) - 1; index >= 0; index--)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                float3 globalPosition = OriginPoint + WydMath.IndexTo3D(index, ChunkController.Size);
                Blocks.SetPoint(globalPosition, GetBlockIDAtPosition(globalPosition, index));
            }

            _NoiseValuesCache.CacheItem(ref _NoiseMap);

            _Stopwatch.Stop();
            TerrainGenerationTimeSpan = _Stopwatch.Elapsed;
        }

        private void GenerateNoise()
        {
            _NoiseMap = _NoiseValuesCache.Retrieve() ?? new float[WydMath.Product(ChunkController.Size)];

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
                _Stopwatch.Restart();

                for (int index = 0; index < WydMath.Product(Blocks.Volume.Size); index++)
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    _NoiseMap[index] =
                        GetNoiseValueByGlobalPosition(OriginPoint + WydMath.IndexTo3D(index, ChunkController.Size));
                }

                _Stopwatch.Stop();
            }

            NoiseRetrievalTimeSpan = _Stopwatch.Elapsed;
        }

        private ushort GetBlockIDAtPosition(float3 globalPosition, int index)
        {
            if ((globalPosition.y < 4) && (globalPosition.y <= SeededRandom.Next(0, 4)))
            {
                return GetCachedBlockID("bedrock");
            }

            if (_NoiseMap[index] < 0.01f)
            {
                return BlockController.AIR_ID;
            }

            // TERRAIN GEN NOTES ON NOISE RANGES
            // Between: 0.0110f to 0.01f = surface crust
            // Between: 0.0105f to 0.01f = grass layer
            // Follows: 0.0105f to 0.0110f = dirt layer

            if (_NoiseMap[index] < 0.0105f)
            {
                return GetCachedBlockID("grass");
            }
            else if (_NoiseMap[index] < 0.011f)
            {
                return GetCachedBlockID("dirt");
            }
            else
            {
                return GetCachedBlockID("stone");
            }
        }

        protected float GetNoiseValueByGlobalPosition(float3 globalPosition)
        {
            float noiseValue = OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, _Frequency, globalPosition);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, WorldController.WORLD_HEIGHT, globalPosition.y));
            noiseValue /= globalPosition.y + (-1f * _Persistence);

            return noiseValue;
        }
    }
}
