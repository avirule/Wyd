#region

using NLog;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.System;
using Wyd.System.Logging;
using Wyd.System.Noise;

#endregion

namespace Wyd.Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJobRawTerrain : ChunkBuildingJob
    {
        public float Frequency;
        public float Persistence;
        public bool GPUAcceleration;
        public ChunkBuilderNoiseValues NoiseValues;

        public void Set(
            Bounds bounds, Block[] blocks, float frequency, float persistence,
            bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null)
        {
            Set(bounds, blocks);

            Frequency = frequency;
            Persistence = persistence;
            GPUAcceleration = gpuAcceleration;

            if ((noiseValuesBuffer != null) && NoiseValuesCache.TryRetrieveItem(out NoiseValues))
            {
                noiseValuesBuffer.GetData(NoiseValues.NoiseValues);
                noiseValuesBuffer.Release();
            }
        }

        protected override void Process()
        {
            Generate(GPUAcceleration, NoiseValues?.NoiseValues);
        }

        protected override void ProcessFinished()
        {
            NoiseValuesCache.CacheItem(ref NoiseValues);
        }

        public void Generate(bool useGpu = false, float[] noiseValues = null)
        {
            if (Blocks == default)
            {
                EventLogger.Log(LogLevel.Error,
                    $"Field `{nameof(Blocks)}` has not been properly set. Cancelling operation.");
                return;
            }

            if (useGpu && (noiseValues == null))
            {
                EventLogger.Log(LogLevel.Warn,
                    $"Parameter `{nameof(useGpu)}` was passed as true, but no noise values were provided. Defaulting to CPU-bound generation.");
                useGpu = false;
            }

            Vector3 position = Vector3.zero;

            BlockController.Current.TryGetBlockId("grass", out ushort blockIdGrass);
            BlockController.Current.TryGetBlockId("dirt", out ushort blockIdDirt);
            BlockController.Current.TryGetBlockId("stone", out ushort blockIdStone);
            BlockController.Current.TryGetBlockId("water", out ushort blockIdWater);
            BlockController.Current.TryGetBlockId("sand", out ushort blockIdSand);


            for (int index = Blocks.Length - 1; (index >= 0) && !AbortToken.IsCancellationRequested; index--)
            {
                (position.x, position.y, position.z) = Mathv.GetIndexAs3D(index, ChunkController.Size);

                if ((position.y < 4) && (position.y <= Rand.Next(0, 4)))
                {
                    BlockController.Current.TryGetBlockId("bedrock", out ushort blockId);
                    Blocks[index].Initialise(blockId);
                }
                else
                {
                    // these seems inefficient, but the CPU branch predictor will pick up on it pretty quick
                    // so the slowdown from this check is nonexistent, since useGpu shouldn't change in this context.
                    float noiseValue = useGpu ? noiseValues[index] : GetNoiseValueByVector3(position);

                    if (noiseValue >= 0.01f)
                    {
                        int indexAbove = index + ChunkController.YIndexStep;

                        if ((position.y > 135) && ((indexAbove > Blocks.Length) || Blocks[indexAbove].Transparent))
                        {
                            Blocks[index].Initialise(blockIdGrass);
                        }
                        else if (IdExistsAboveWithinRange(index, 2, blockIdGrass))
                        {
                            Blocks[index].Initialise(blockIdDirt);
                        }
                        else
                        {
                            Blocks[index].Initialise(blockIdStone);
                        }
                    }
                    else if ((position.y <= 155) && (position.y > 135))
                    {
                        Blocks[index].Initialise(blockIdWater);
                    }
                    else
                    {
                        Blocks[index] = default;
                    }
                }
            }
        }

        protected float GetNoiseValueByVector3(Vector3 pos3d)
        {
            float noiseValue = OpenSimplex_FastNoise.GetSimplex(WorldController.Current.Seed, Frequency,
                Bounds.min.x + pos3d.x, Bounds.min.y + pos3d.y, Bounds.min.z + pos3d.z);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, ChunkController.Size.y, pos3d.y));
            noiseValue /= pos3d.y + (-1f * Persistence);

            return noiseValue;
        }
    }
}
