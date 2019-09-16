#region

using Controllers.World;
using Game.World.Blocks;
using Logging;
using NLog;
using Noise;
using UnityEngine;

#endregion

namespace Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJobRawTerrain : ChunkBuildingJob
    {
        public float Frequency;
        public float Persistence;
        public bool GPUAcceleration;
        public ChunkBuilderNoiseValues NoiseValues;

        public void Set(
            Vector3 position, Block[] blocks, float frequency, float persistence,
            bool gpuAcceleration = false, ComputeBuffer noiseValuesBuffer = null)
        {
            Set(position, blocks);

            Frequency = frequency;
            Persistence = persistence;
            GPUAcceleration = gpuAcceleration;

            if (noiseValuesBuffer != null)
            {
                NoiseValues = NoiseValuesCache.RetrieveItem();
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
                EventLog.Logger.Log(LogLevel.Error,
                    $"Field `{nameof(Blocks)}` has not been properly set. Cancelling operation.");
                return;
            }

            if (useGpu && (noiseValues == null))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Parameter `{nameof(useGpu)}` was passed as true, but no noise values were provided. Defaulting to CPU-bound generation.");
                useGpu = false;
            }

            Vector3 position = Vector3.zero;

            for (int index = Blocks.Length - 1; (index >= 0) && !AbortToken.IsCancellationRequested; index--)
            {
                (position.x, position.y, position.z) = Mathv.GetVector3IntIndex(index, ChunkController.Size);

                if ((position.y < 4) && (position.y <= Rand.Next(0, 4)))
                {
                    Blocks[index].Initialise(TerrainGenerationIds["bedrock"]);
                }
                else
                {
                    // these seems inefficient, but the CPU branch predictor will pick up on it pretty quick
                    // so the slowdown from this check is nonexistent, since useGpu shouldn't change in this context.
                    float noiseValue = useGpu ? noiseValues[index] : GetNoiseValueByVector3(position);
                    ProcessPreGeneratedNoiseData(index, position, noiseValue);
                }
            }
        }

        public void ProcessPreGeneratedNoiseData(int index, Vector3 position, float noiseValue)
        {
            if (noiseValue >= 0.01f)
            {
                int indexAbove = index + ChunkController.YIndexStep;

                if ((position.y >= 130) && Blocks[indexAbove].Transparent)
                {
                    Blocks[index].Initialise(TerrainGenerationIds["grass"]);
                }
                else if (IdExistsAboveWithinRange(index, 4, TerrainGenerationIds["grass"]))
                {
                    Blocks[index].Initialise(TerrainGenerationIds["dirt"]);
                }
                else
                {
                    Blocks[index].Initialise(TerrainGenerationIds["stone"]);
                }
            }
            else
            {
                Blocks[index] = default;
            }
        }

        protected float GetNoiseValueByVector3(Vector3 pos3d)
        {
            float noiseValue = OpenSimplex_FastNoise.GetSimplex(WorldController.Current.Seed, Frequency,
                Position.x + pos3d.x, Position.y + pos3d.y, Position.z + pos3d.z);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, ChunkController.Size.y, pos3d.y));
            noiseValue /= pos3d.y + (-1f * Persistence);

            return noiseValue;
        }
    }
}