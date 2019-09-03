#region

using Controllers.Game;
using Controllers.World;
using Game.World.Chunk;
using Noise.OpenSimplex;
using UnityEngine;
using Random = System.Random;

#endregion

namespace Threading
{
    public class ChunkBuildingThreadedItem : ThreadedItem
    {
        private static readonly OpenSimplexNoise NoiseFunction =
            new OpenSimplexNoise(WorldController.Current.WorldGenerationSettings.Seed);

        private Random _Rand;
        private Vector3 _Position;
        private ushort[] _Blocks;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        public void Set(Vector3 position, ushort[] blocks)
        {
            _Rand = new Random(WorldController.Current.WorldGenerationSettings.Seed);
            _Position = position;
            _Blocks = blocks;
        }

        protected override void Process()
        {
            if (_Blocks == default)
            {
                return;
            }

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
                    _Blocks[index] = BlockController.Current.GetBlockId("Stone");
                }
                else
                {
                    _Blocks[index] = BlockController.Current.GetBlockId("Dirt");
                }
            }
            else
            {
                if ((z % 2) == 0)
                {
                    _Blocks[index] = BlockController.Current.GetBlockId("Dirt");
                }
                else
                {
                    _Blocks[index] = BlockController.Current.GetBlockId("Stone");
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
                _Blocks[index] = BlockController.Current.GetBlockId("Grass");
            }
            else if (y < halfSize)
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Stone");
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
                _Blocks[index] = BlockController.Current.GetBlockId("Grass");
            }
            else
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Stone");
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
                _Blocks[index] = BlockController.Current.GetBlockId("Stone");
            }
            else
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Dirt");
            }
        }

        private void Generate3DSimplex(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            if ((y < 4) && (y <= _Rand.Next(0, 4)))
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Bedrock");
            }
            else
            {
                double noiseValue =
                    NoiseFunction.Evaluate((_Position.x + x) / 20d, (_Position.y + y) / 20d, (_Position.z + z) / 20d);
                noiseValue /= y;

                if (noiseValue >= 0.01d)
                {
                    _Blocks[index] = BlockController.Current.GetBlockId("Grass");
                }
            }
        }
    }
}