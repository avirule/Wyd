#region

using System.Collections.Generic;
using Serilog;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.System;
using Wyd.System.Compression;
using Wyd.System.Noise;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkRawTerrainBuilder : ChunkBuilder
    {
        private ComputeBuffer _NoiseValuesBuffer;
        private float _Frequency;
        private float _Persistence;
        private bool _GpuAcceleration;
        private ChunkBuilderNoiseValues _NoiseValues;

        public void SetData(GenerationData generationData, ComputeBuffer noiseValuesBuffer, float frequency,
            float persistence,
            bool gpuAcceleration = false)
        {
            SetGenerationData(generationData);
            _Frequency = frequency;
            _Persistence = persistence;
            _GpuAcceleration = gpuAcceleration;

            if (_GpuAcceleration)
            {
                Log.Warning(
                    $"Parameter `{nameof(_GpuAcceleration)}` is set to true, but no noise values were provided. Defaulting to CPU-bound generation.");
                _GpuAcceleration = false;
            }

            if (noiseValuesBuffer != null)
            {
                _NoiseValues = NoiseValuesCache.RetrieveItem() ?? new ChunkBuilderNoiseValues();
                noiseValuesBuffer.GetData(_NoiseValues.NoiseValues);
                noiseValuesBuffer.Release();
            }
            else if (_GpuAcceleration)
            {
                Log.Warning(
                    $"Parameter `{nameof(_GpuAcceleration)}` is set to true, but no noise values were provided. Defaulting to CPU-bound generation.");
                _GpuAcceleration = false;
            }
        }

        public void Generate(float[] noiseValues = null)
        {
            if (_GenerationData.Blocks == default)
            {
                Log.Error(
                    $"Field `{nameof(_GenerationData.Blocks)}` has not been properly set. Cancelling operation.");
                return;
            }

            _GenerationData.Blocks.Clear();

            bool firstIteration = true;
            ushort currentId = 0;
            uint runLength = 0;

            for (int index = ChunkController.SizeProduct - 1;
                (index >= 0) && !AbortToken.IsCancellationRequested;
                index--)
            {
                (int x, int y, int z) = Mathv.GetIndexAs3D(index, ChunkController.Size);

                ushort nextId = GetBlockToGenerate(new Vector3Int(x, y, z), index, _GpuAcceleration, noiseValues);

                if (firstIteration)
                {
                    currentId = nextId;
                    runLength += 1;
                    firstIteration = false;
                }

                if (currentId == nextId)
                {
                    runLength += 1;
                }
                else
                {
                    AddBlockSequentialAware(currentId, runLength);
                    // set current id to new current block
                    currentId = nextId;
                    // reset run length
                    runLength = 1;
                }
            }
        }

        private ushort GetBlockToGenerate(Vector3Int position, int index, bool useGpu = false,
            IReadOnlyList<float> noiseValues = null) =>
            position.y == 1 ? (ushort)1 : BlockController.AIR_ID;

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
                _GenerationData.Bounds.min.x + pos3d.x, _GenerationData.Bounds.min.y + pos3d.y,
                _GenerationData.Bounds.min.z + pos3d.z);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, ChunkController.Size.y, pos3d.y));
            noiseValue /= pos3d.y + (-1f * _Persistence);

            return noiseValue;
        }

        private void AddBlockSequentialAware(ushort blockId, uint runLength)
        {
            // allocate from the front since we are adding from top to bottom (i.e. last to first)
            if (_GenerationData.Blocks.Count > 0)
            {
                LinkedListNode<RLENode<ushort>> firstNode = _GenerationData.Blocks.First;

                if (firstNode.Value.Value == blockId)
                {
                    firstNode.Value.RunLength += runLength;
                }
                else
                {
                    _GenerationData.Blocks.AddFirst(new RLENode<ushort>(runLength, blockId));
                }
            }
            else
            {
                _GenerationData.Blocks.AddFirst(new RLENode<ushort>(runLength, blockId));
            }
        }
    }
}
