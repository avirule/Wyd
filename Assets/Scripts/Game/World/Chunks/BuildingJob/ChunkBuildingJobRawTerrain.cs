#region

using System.Collections.Generic;
using System.Linq;
using Serilog;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.System;
using Wyd.System.Compression;
using Wyd.System.Noise;

#endregion

namespace Wyd.Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJobRawTerrain : ChunkBuildingJob
    {
        private ushort _BlockIdBedrock;
        private ushort _BlockIdGrass;
        private ushort _BlockIdDirt;
        private ushort _BlockIdStone;
        private ushort _BlockIdWater;
        private ushort _BlockIdSand;

        public float Frequency;
        public float Persistence;
        public bool GpuAcceleration;
        public ChunkBuilderNoiseValues NoiseValues;

        public void Set(Bounds bounds, ref LinkedList<RLENode<ushort>> blocks, float frequency, float persistence,
            bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null)
        {
            Set(bounds, ref blocks);

            Frequency = frequency;
            Persistence = persistence;
            GpuAcceleration = gpuAcceleration;

            if (noiseValuesBuffer != null)
            {
                NoiseValues = NoiseValuesCache.RetrieveItem() ?? new ChunkBuilderNoiseValues();
                noiseValuesBuffer.GetData(NoiseValues.NoiseValues);
                noiseValuesBuffer.Release();
            }

            BlockController.Current.TryGetBlockId("bedrock", out _BlockIdBedrock);
            BlockController.Current.TryGetBlockId("grass", out _BlockIdGrass);
            BlockController.Current.TryGetBlockId("dirt", out _BlockIdDirt);
            BlockController.Current.TryGetBlockId("stone", out _BlockIdStone);
            BlockController.Current.TryGetBlockId("water", out _BlockIdWater);
            BlockController.Current.TryGetBlockId("sand", out _BlockIdSand);
        }

        protected override void Process()
        {
            Generate(GpuAcceleration, NoiseValues?.NoiseValues);
        }

        protected override void ProcessFinished()
        {
            NoiseValuesCache.CacheItem(ref NoiseValues);
        }

        public void Generate(bool useGpu = false, float[] noiseValues = null)
        {
            if (_Blocks == default)
            {
                Log.Error(
                    $"Field `{nameof(_Blocks)}` has not been properly set. Cancelling operation.");
                return;
            }

            if (useGpu && (noiseValues == null))
            {
                Log.Warning(
                    $"Parameter `{nameof(useGpu)}` was passed as true, but no noise values were provided. Defaulting to CPU-bound generation.");
                useGpu = false;
            }

            _Blocks.Clear();

            bool firstIteration = true;
            ushort currentId = 0;
            uint runLength = 0;

            for (int index = ChunkController.SizeProduct - 1;
                (index >= 0) && !AbortToken.IsCancellationRequested;
                index--)
            {
                (int x, int y, int z) = Mathv.GetIndexAs3D(index, ChunkController.Size);

                ushort nextId = GetBlockToGenerate(new Vector3Int(x, y, z), index, useGpu, noiseValues);
                LocalBlocksCache.Add(nextId);

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

            LocalBlocksCache.Clear();
        }

        private ushort GetBlockToGenerate(Vector3Int position, int index, bool useGpu = false,
            IReadOnlyList<float> noiseValues = null)
        {
//            if ((position.y < 4) && (position.y <= _Rand.Next(0, 4)))
//            {
//                return _BlockIdBedrock;
//            }

            // add non-local values to current stack
            int sizeProduct = ChunkController.SizeProduct;
            int yIndexStep = ChunkController.YIndexStep;

            // these seems inefficient, but the CPU branch predictor will pick up on it pretty quick
            // so the slowdown from this check is nonexistent, since useGpu shouldn't change in this context.
            float noiseValue = useGpu ? noiseValues[index] : GetNoiseValueByVector3(position);

            if (position.y == 1)
            {
                return _BlockIdGrass;
            }
            
//            if (noiseValue >= 0.01f)
//            {
//                int indexAbove = index + yIndexStep;
//
//                if ((position.y > 135)
//                    && BlockController.Current.CheckBlockHasProperty(LocalBlocksCache[sizeProduct - indexAbove],
//                        BlockRule.Property.Transparent))
//                {
//                    return _BlockIdGrass;
//                }
//                // todo fix this
//                // else if (IdExistsAboveWithinRange(index, 2, blockIdGrass))
//                // {
//                //     AddBlockSequentialAware(blockIdDirt);
//                // }
//                else
//                {
//                    return _BlockIdStone;
//                }
//            }
//            else if ((position.y <= 155) && (position.y > 135))
//            {
//                return _BlockIdWater;
//            }

            return BlockController.AIR_ID;
        }

        protected float GetNoiseValueByVector3(Vector3 pos3d)
        {
            float noiseValue = OpenSimplex_FastNoise.GetSimplex(WorldController.Current.Seed, Frequency,
                _Bounds.min.x + pos3d.x, _Bounds.min.y + pos3d.y, _Bounds.min.z + pos3d.z);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, ChunkController.Size.y, pos3d.y));
            noiseValue /= pos3d.y + (-1f * Persistence);

            return noiseValue;
        }

        private void AddBlockSequentialAware(ushort blockId, uint runLength)
        {
            // allocate from the front since we are adding from top to bottom (i.e. last to first)
            if (_Blocks.Count > 0)
            {
                LinkedListNode<RLENode<ushort>> firstNode = _Blocks.First;

                if (firstNode.Value.Value == blockId)
                {
                    firstNode.Value.RunLength += runLength;
                }
                else
                {
                    _Blocks.AddFirst(new RLENode<ushort>(runLength, blockId));
                }
            }
            else
            {
                _Blocks.AddFirst(new RLENode<ushort>(runLength, blockId));
            }
        }
    }
}
