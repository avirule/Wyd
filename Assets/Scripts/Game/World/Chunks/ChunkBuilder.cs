#region

using System.Threading;
using Controllers.Game;
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
        private static FastNoise _noiseFunction;

        public CancellationToken AbortToken;
        public Random Rand;
        public Vector3 Position;
        public Block[] Blocks;

        public ChunkBuilder()
        {
            if (_noiseFunction == default)
            {
                _noiseFunction = new FastNoise(WorldController.Current.WorldGenerationSettings.Seed);
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
            Rand = new Random(WorldController.Current.WorldGenerationSettings.Seed);
            Position.Set(position.x, position.y, position.z);
            Blocks = blocks;
        }

        public void GenerateMemorySensitive()
        {
            if (Blocks == default)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Field `{nameof(Blocks)}` has not been properly set. Cancelling operation.");
                return;
            }

            for (int index = 0; (index < Blocks.Length) && !AbortToken.IsCancellationRequested; index++)
            {
                Generate3DSimplex(index);
            }

            for (int index = 0; (index < Blocks.Length) && !AbortToken.IsCancellationRequested; index++)
            {
                GenerateGrass(index);
            }
        }

        public void ProcessPreGeneratedNoiseData(float[] noiseValues)
        {
            if (Blocks == default)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Field `{nameof(Blocks)}` has not been properly set. Cancelling operation.");
                return;
            }

            for (int index = 0; (index < noiseValues.Length) && !AbortToken.IsCancellationRequested; index++)
            {
                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

                if ((y < 4) && (y <= Rand.Next(0, 4)))
                {
                    Blocks[index].Initialise(BlockController.Current.GetBlockId("Bedrock"));
                }
                else
                {
                    if (noiseValues[index] >= 0.01f)
                    {
                        Blocks[index].Initialise(BlockController.Current.GetBlockId("stone"));
                    }
                }
            }
        }

#if UNITY_EDITOR

        private void GenerateCheckerBoard(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

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
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            int halfSize = Chunk.Size.y / 2;

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
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            int halfSize = Chunk.Size.y / 2;

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
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

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

        private void Generate3DSimplex(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            if ((y < 4) && (y <= Rand.Next(0, 4)))
            {
                Blocks[index].Initialise(BlockController.Current.GetBlockId("Bedrock"));
            }
            else
            {
                float noiseValue = _noiseFunction.GetSimplex(Position.x + x, Position.y + y, Position.z + z);
                noiseValue /= y;

                if (noiseValue >= 0.01f)
                {
                    Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
                }
                else
                {
                    Blocks[index] = default;
                }
            }
        }

        private void GenerateGrass(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            int indexAbove = index + (Chunk.Size.x * Chunk.Size.z);

            if (indexAbove >= Blocks.Length
                || (Blocks[index].Id == BlockController.Current.GetBlockId("Bedrock"))
                || Blocks[index].Transparent
                || !Blocks[indexAbove].Transparent)
            {
                return;
            }

            Blocks[index].Initialise(BlockController.Current.GetBlockId("Grass"));

            for (int i = 1; i < 4; i++)
            {
                int currentIndex = index - (i * Chunk.Size.x * Chunk.Size.z);

                if ((currentIndex < 0) || Blocks[currentIndex].Transparent)
                {
                    continue;
                }

                Blocks[currentIndex].Initialise(BlockController.Current.GetBlockId("Dirt"));
            }
        }
    }
}