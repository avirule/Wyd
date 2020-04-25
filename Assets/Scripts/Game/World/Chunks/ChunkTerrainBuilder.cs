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

        public ChunkTerrainBuilder(CancellationToken cancellationToken, float3 originPoint, float frequency,
            float persistence, ComputeBuffer noiseValuesBuffer)
            : base(cancellationToken, originPoint)
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
            _NoiseMap = _noiseValuesCache.Retrieve() ?? new float[ChunkController.SIZE_CUBED];

            if (_NoiseValuesBuffer == null)
            {
                for (int index = 0; index < _NoiseMap.Length; index++)
                {
                    float3 globalPosition = OriginPoint + WydMath.IndexTo3D(index, ChunkController.SIZE);

                    _NoiseMap[index] = GetNoiseValueByGlobalPosition(globalPosition);
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

                if (_NoiseMap[index] < 0.01f)
                {
                    allStone = false;
                }
            }

            if (allStone)
            {
                _Blocks = new OctreeNode<ushort>(ChunkController.SIZE, GetCachedBlockID("stone"));
                return;
            }

            _Blocks = new OctreeNode<ushort>(ChunkController.SIZE, BlockController.AirID);

            if (allAir)
            {
                return;
            }

            for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (_NoiseMap[index] < 0.01f)
                {
                    continue; // air
                }

                float3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);
                float3 globalPosition = OriginPoint + localPosition;

                if ((globalPosition.y < 4) && (globalPosition.y <= SeededRandom.Next(0, 4)))
                {
                    _Blocks.SetPoint(localPosition, GetCachedBlockID("bedrock"));
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
