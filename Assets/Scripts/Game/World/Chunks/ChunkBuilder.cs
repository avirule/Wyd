#region

using System.Collections.Generic;
using System.Threading;
using Controllers.State;
using Controllers.World;
using Game.World.Blocks;
using Logging;
using NLog;
using Noise;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Game.World.Chunks
{
    public class ChunkBuilder
    {
        private static readonly string[] BlocksUsedForTerrainGeneration =
        {
            "grass",
            "dirt",
            "stone",
            "bedrock"
        };

        private static OpenSimplex_FastNoise _noiseFunction;
        private static Dictionary<string, ushort> _terrainGenerationIds;

        public CancellationToken AbortToken;
        public Random Rand;
        public Vector3 Position;
        public Block[] Blocks;
        public float Frequency;
        public float Persistence;

        static ChunkBuilder()
        {
            FillTerrainGenerationIds();
        }

        public ChunkBuilder()
        {
            if (_noiseFunction == default)
            {
                _noiseFunction = new OpenSimplex_FastNoise(WorldController.Current.Seed);
            }
        }

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="abortToken"></param>
        public ChunkBuilder(Vector3 position, Block[] blocks, CancellationToken abortToken) : this()
        {
            AbortToken = abortToken;
            Rand = new Random(WorldController.Current.Seed);
            Position.Set(position.x, position.y, position.z);
            Blocks = blocks;
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
                    Blocks[index].Initialise(_terrainGenerationIds["bedrock"]);
                }
                else
                {
                    // these seems inefficient, but the CPU branch predictor will pick up on it pretty quick
                    // so the slowdown from this check is nonexistent, since useGpu shouldn't change in this context.
                    float noiseValue = useGpu ? noiseValues[index] : GetNoiseValueByVector3(position);
                    ProcessPreGeneratedNoiseData(index, position, noiseValue);
                }
            }

            // get list of block ids that should be ignored on second generation pass
        }

        public void ProcessPreGeneratedNoiseData(int index, Vector3 position, float noiseValue)
        {
            if (noiseValue >= 0.01f)
            {
                int indexAbove = index + ChunkController.YIndexStep;

                if ((position.y >= 130) && Blocks[indexAbove].Transparent)
                {
                    Blocks[index].Initialise(_terrainGenerationIds["grass"]);
                }
                else if (IdExistsAboveWithinRange(index, 4, _terrainGenerationIds["grass"]))
                {
                    Blocks[index].Initialise(_terrainGenerationIds["dirt"]);
                }
                else
                {
                    Blocks[index].Initialise(_terrainGenerationIds["stone"]);
                }
            }
            else
            {
                Blocks[index] = default;
            }
        }

        private static void FillTerrainGenerationIds()
        {
            _terrainGenerationIds = new Dictionary<string, ushort>();

            foreach (string blockName in BlocksUsedForTerrainGeneration)
            {
                BlockController.Current.TryGetBlockId(blockName, out ushort blockId);
                _terrainGenerationIds.Add(blockName, blockId);
            }
        }

        private float GetNoiseValueByVector3(Vector3 pos3d)
        {
            float noiseValue = OpenSimplex_FastNoise.GetSimplex(WorldController.Current.Seed, Frequency,
                Position.x + pos3d.x, Position.y + pos3d.y, Position.z + pos3d.z);
            noiseValue += 5f * (1f - Mathf.InverseLerp(0f, ChunkController.Size.y, pos3d.y));
            noiseValue /= pos3d.y + (-1f * Persistence);

            return noiseValue;
        }

        private bool IdExistsAboveWithinRange(int startIndex, int maxSteps, ushort soughtId)
        {
            for (int i = 1; i < (maxSteps + 1); i++)
            {
                int currentIndex = startIndex + (i * ChunkController.YIndexStep);

                if (currentIndex > Blocks.Length)
                {
                    return false;
                }

                if (Blocks[currentIndex].Id == soughtId)
                {
                    return true;
                }
            }

            return false;
        }

        #region DEBUG GEN MODES

#if UNITY_EDITOR

        private void GenerateCheckerBoard(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, ChunkController.Size);

            if (y != 0)
            {
                return;
            }

            if ((x % 2) == 0)
            {
                if ((z % 2) == 0)
                {
                    Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
                }
                else
                {
                    Blocks[index].Initialise(BlockController.Current.GetBlockId("Dirt"));
                }
            }
            else
            {
                if ((z % 2) == 0)
                {
                    Blocks[index].Initialise(BlockController.Current.GetBlockId("Dirt"));
                }
                else
                {
                    Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
                }
            }
        }

        private void GenerateRaisedStripes(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, ChunkController.Size);

            int halfSize = ChunkController.Size.y / 2;

            if (y > halfSize)
            {
                return;
            }

            if (((y == halfSize) && ((x % 2) == 0)) || ((y == (halfSize - 1)) && ((x % 2) != 0)))
            {
                Blocks[index].Initialise(BlockController.Current.GetBlockId("Grass"));
            }
            else if (y < halfSize)
            {
                Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
            }
        }

        private void GenerateFlat(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, ChunkController.Size);

            int halfSize = ChunkController.Size.y / 2;

            if (y > halfSize)
            {
                return;
            }

            if (y == halfSize)
            {
                Blocks[index].Initialise(BlockController.Current.GetBlockId("Grass"));
            }
            else
            {
                Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
            }
        }

        private void GenerateFlatStriped(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, ChunkController.Size);

            if (y != 0)
            {
                return;
            }

            if ((x % 2) == 0)
            {
                Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
            }
            else
            {
                Blocks[index].Initialise(BlockController.Current.GetBlockId("Dirt"));
            }
        }

#endif

        #endregion
    }
}
