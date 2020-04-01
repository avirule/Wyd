#region

using System;
using System.Diagnostics;
using System.Threading;
using Serilog;
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
            _NoiseValuesBuffer.GetData(_NoiseValues.NoiseValues);
            _NoiseValuesBuffer.Release();
            NoiseValuesReady = true;
        }

        public void Generate()
        {
            if (_GenerationData.Blocks == default)
            {
                Log.Error($"`{nameof(_GenerationData.Blocks)}` has not been set. Aborting generation.");
                return;
            }

            if (_GpuAcceleration && (_NoiseValuesBuffer != null))
            {
                _Stopwatch.Restart();

                _NoiseValues = NoiseValuesCache.Retrieve() ?? new ChunkBuilderNoiseValues();

                ManualResetEvent manualResetEvent = new ManualResetEvent(false);
                MainThreadActionsController.Current.PushAction(new MainThreadAction(manualResetEvent,
                    GetComputeBufferData));
                manualResetEvent.WaitOne();

                _Stopwatch.Stop();
                NoiseRetrievalTimeSpan = _Stopwatch.Elapsed;
            }
            else if (_GpuAcceleration && (_NoiseValuesBuffer == null))
            {
                Log.Warning(
                    $"`{nameof(_GpuAcceleration)}` is set to true, but no noise values were provided. Defaulting to CPU-bound generation.");
                _GpuAcceleration = false;
            }

            _Stopwatch.Restart();
            _GenerationData.Blocks.Collapse(true);
            Vector3 position = _GenerationData.Bounds.min;

            for (int index = ChunkController.SizeProduct - 1; index >= 0; index--)
            {
                Vector3 globalPosition = position + Mathv.GetIndexAsVector3Int(index, ChunkController.Size);
                //ushort id = _NoiseValues.NoiseValues[index] > ChunkTerrainController.THRESHOLD ? (ushort)1 : BlockController.AIR_ID;

                ushort id = 0;

                if (_NoiseValues.NoiseValues[index] > 0.01f)
                {
                    if (_NoiseValues.NoiseValues[index] < 0.005f)
                    {
                        id = 2;
                    }
                    else
                    {
                        id = 3;
                    }
                }

                _GenerationData.Blocks.SetPoint(globalPosition, id);
            }

            NoiseValuesCache.CacheItem(ref _NoiseValues);

            _Stopwatch.Stop();
            TerrainGenerationTimeSpan = _Stopwatch.Elapsed;
        }

        //            if ((position.y < 4) && (position.y <= _Rand.Next(0, 4)))
        //            {
        //                return _BlockIdBedrock;
        //            }
        // // add non-local values to current stack
        // int sizeProduct = ChunkController.SizeProduct;
        // int yIndexStep = ChunkController.YIndexStep;
        //
        // // these seems inefficient, but the CPU branch predictor will pick up on it pretty quick
        // // so the slowdown from this check is nonexistent, since useGpu shouldn't change in this context.
        // float noiseValue = useGpu ? noiseValues[index] : GetNoiseValueByVector3(position);
        //
        // if (noiseValue >= 0.01f)
        // {
        //     int indexAbove = index + yIndexStep;
        //
        //     if ((position.y > 135)
        //         && BlockController.Current.CheckBlockHasProperty(LocalBlocksCache[sizeProduct - indexAbove],
        //             BlockRule.Property.Transparent))
        //     {
        //         return _BlockIdGrass;
        //     }
        //     // todo fix this
        //     // else if (IdExistsAboveWithinRange(index, 2, blockIdGrass))
        //     // {
        //     //     AddBlockSequentialAware(blockIdDirt);
        //     // }
        //     else
        //     {
        //         return _BlockIdStone;
        //     }
        // }
        // else if ((position.y <= 155) && (position.y > 135))
        // {
        //     return _BlockIdWater;
        // }
        //
        // return BlockController.AIR_ID;

        protected float GetNoiseValueByVector3(Vector3 pos3d)
        {
            float noiseValue = OpenSimplex_FastNoise.GetSimplex(WorldController.Current.Seed, _Frequency,
                pos3d.x, pos3d.y, pos3d.z);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, ChunkController.Size.y, pos3d.y));
            noiseValue /= pos3d.y + (-1f * _Persistence);

            return noiseValue;
        }
    }
}
