#region

using System;
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
    public class ChunkBuilder : IDisposable
    {
        private static FastNoise _noiseFunction;

        public CancellationToken AbortToken;
        public Random Rand;
        public Vector3 Position;
        public Block[] Blocks;
        public bool MemoryNegligent;
        public ComputeShader NoiseShader;
        public ComputeBuffer NoiseBuffer;
        public float[] NoiseValues;

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
        /// <param name="memoryNegligent"></param>
        /// <param name="noiseShader"></param>
        public ChunkBuilder(Vector3 position, Block[] blocks, CancellationToken abortToken,
            bool memoryNegligent = false, ComputeShader noiseShader = null) : this()
        {
            AbortToken = abortToken;
            Rand = new Random(WorldController.Current.WorldGenerationSettings.Seed);
            Position.Set(position.x, position.y, position.z);
            Blocks = blocks;
            MemoryNegligent = memoryNegligent;
            NoiseShader = noiseShader;
        }

        public void GenerateMemoryNegligent()
        {
            if (NoiseShader == default)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Field `{nameof(NoiseShader)}` has not been properly set. Defaulting to memory-sensitive execution.");
                MemoryNegligent = false;
                return;
            }

            if (NoiseBuffer == default)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Field `{nameof(NoiseBuffer)}` has not been properly set. Defaulting to memory-sensitive execution.");
                MemoryNegligent = false;
                return;
            }

            if (NoiseValues == default)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Field `{nameof(NoiseValues)}` has not been properly set. Defaulting to memory-sensitive execution.");
                MemoryNegligent = false;
                return;
            }

            InitializeMemoryNegligentComputationResources();
            ExecuteNoiseMappingOnGpu();
            ProcessPreGeneratedNoiseData();
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
                //GenerateCheckerBoard(index);
                //GenerateRaisedStripes(index);
                //GenerateFlat(index);
                //GenerateFlatStriped(index);
                //GenerateNormal(index);
                Generate3DSimplex(index);
            }
        }


        private void InitializeMemoryNegligentComputationResources()
        {
            // stride 4 bytes for float values
            NoiseBuffer = new ComputeBuffer(Blocks.Length, 4);
            NoiseValues = new float[Blocks.Length];
        }

        private void ExecuteNoiseMappingOnGpu()
        {
            int kernel = NoiseShader.FindKernel("CSMain");
            NoiseShader.SetBuffer(kernel, "Result", NoiseBuffer);
            NoiseShader.Dispatch(kernel, NoiseValues.Length / 16, 1, 1);
            NoiseBuffer.GetData(NoiseValues);
        }

        private void ProcessPreGeneratedNoiseData()
        {
            for (int index = 0; index < NoiseValues.Length; index++)
            {
                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

                if ((y < 4) && (y <= Rand.Next(0, 4)))
                {
                    Blocks[index].Initialise(BlockController.Current.GetBlockId("Bedrock"));
                }
                else
                {
                    if (NoiseValues[index] >= 0.01f)
                    {
                        Blocks[index].Initialise(BlockController.Current.GetBlockId("stone"));
                    }
                }
            }
        }

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
            }
        }

        public void Dispose()
        {
            NoiseBuffer?.Dispose();
        }
    }
}