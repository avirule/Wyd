#region

using System;
using Controllers.Game;
using Controllers.World;
using Game.World.Blocks;
using Game.World.Chunks;
using Logging;
using NLog;
using Noise;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Threading
{
    public class ChunkBuildingThreadedItem : ThreadedItem, IDisposable
    {
        private static FastNoise _noiseFunction;

        private Random _Rand;
        private Vector3 _Position;
        private Block[] _Blocks;
        private bool _MemoryNegligent;
        private ComputeShader _NoiseShader;
        private ComputeBuffer _NoiseBuffer;
        private float[] _NoiseValues;


        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="memoryNegligent"></param>
        /// <param name="noiseShader"></param>
        public void Set(Vector3 position, Block[] blocks, bool memoryNegligent = false,
            ComputeShader noiseShader = null)
        {
            if (_noiseFunction == default)
            {
                _noiseFunction = new FastNoise(WorldController.Current.WorldGenerationSettings.Seed);
            }


            _Rand = new Random(WorldController.Current.WorldGenerationSettings.Seed);
            _Position.Set(position.x, position.y, position.z);
            _Blocks = blocks;
            _MemoryNegligent = memoryNegligent;
            _NoiseShader = noiseShader;
        }

        protected override void Process()
        {
            if (_Blocks == default)
            {
                return;
            }

            if (_MemoryNegligent)
            {
                if (_NoiseShader == null)
                {
                    EventLog.Logger.Log(LogLevel.Error,
                        $"Field `{nameof(_NoiseShader)}` has not been properly set. Defaulting to memory-sensitive execution.");
                    _MemoryNegligent = false;
                    return;
                }

                InitializeMemoryNegligentComputationResources();
                ExecuteNoiseMappingOnGPU();
            }


            // split the if to be conscious of any errors found in the initial statement above
            if (_MemoryNegligent)
            {
                ProcessPreGeneratedNoiseData();
            }
            else
            {
                for (int index = 0; index < _Blocks.Length; index++)
                {
                    if (AbortToken.IsCancellationRequested)
                    {
                        return;
                    }

                    //GenerateCheckerBoard(index);
                    //GenerateRaisedStripes(index);
                    //GenerateFlat(index);
                    //GenerateFlatStriped(index);
                    //GenerateNormal(index);
                    Generate3DSimplex(index);
                }
            }
        }

        private void InitializeMemoryNegligentComputationResources()
        {
            // stride 4 bytes for float values
            _NoiseBuffer = new ComputeBuffer(_Blocks.Length, 4);
            _NoiseValues = new float[_Blocks.Length];
        }

        private void ExecuteNoiseMappingOnGPU()
        {
            int kernel = _NoiseShader.FindKernel("CSMain");
            _NoiseShader.SetBuffer(kernel, "Result", _NoiseBuffer);
            _NoiseShader.Dispatch(kernel, _NoiseValues.Length / 16, 1, 1);
            _NoiseBuffer.GetData(_NoiseValues);
        }

        private void ProcessPreGeneratedNoiseData()
        {
            for (int index = 0; index < _NoiseValues.Length; index++)
            {
                (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

                if ((y < 4) && (y <= _Rand.Next(0, 4)))
                {
                    _Blocks[index].Initialise(BlockController.Current.GetBlockId("Bedrock"));
                }
                else
                {
                    if (_NoiseValues[index] >= 0.01f)
                    {
                        _Blocks[index].Initialise(BlockController.Current.GetBlockId("stone"));
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
                    _Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
                }
                else
                {
                    _Blocks[index].Initialise(BlockController.Current.GetBlockId("Dirt"));
                }
            }
            else
            {
                if ((z % 2) == 0)
                {
                    _Blocks[index].Initialise(BlockController.Current.GetBlockId("Dirt"));
                }
                else
                {
                    _Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
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
                _Blocks[index].Initialise(BlockController.Current.GetBlockId("Grass"));
            }
            else if (y < halfSize)
            {
                _Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
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
                _Blocks[index].Initialise(BlockController.Current.GetBlockId("Grass"));
            }
            else
            {
                _Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
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
                _Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
            }
            else
            {
                _Blocks[index].Initialise(BlockController.Current.GetBlockId("Dirt"));
            }
        }

        private void Generate3DSimplex(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            if ((y < 4) && (y <= _Rand.Next(0, 4)))
            {
                _Blocks[index].Initialise(BlockController.Current.GetBlockId("Bedrock"));
            }
            else
            {
                float noiseValue = _noiseFunction.GetSimplex(_Position.x + x, _Position.y + y, _Position.z + z);
                noiseValue /= y * 1.5f;

                if (noiseValue >= 0.01f)
                {
                    _Blocks[index].Initialise(BlockController.Current.GetBlockId("Stone"));
                }
            }
        }

        public void Dispose()
        {
            _NoiseBuffer?.Dispose();
        }
    }
}