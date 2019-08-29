#region

using Controllers.Game;
using Controllers.World;
using Game.World;
using Noise.OpenSimplex;
using Static;
using UnityEngine;

#endregion

namespace Threading
{
    public class ChunkBuildingThreadedItem : ThreadedItem
    {
        private readonly Vector3Int Position;
        private readonly ushort[] _Blocks;
        private readonly float[] _NoiseMap;

        /// <summary>
        ///     Initialises a new instance of the <see cref="Threading.ChunkBuildingThreadedItem" /> class.
        /// </summary>
        /// <param name="blocks">Pre-initialized <see cref="T:Block[]" /> of blocks to iterate through.</param>
        /// <param name="noiseMap">Pre-initialized <see cref="T:float[][]" /> of noise values.</param>
        /// <seealso cref="Threading.ChunkMeshingThreadedItem" />
        public ChunkBuildingThreadedItem(ref Vector3Int position, ref ushort[] blocks, float[] noiseMap)
        {
            Position = position;
            _Blocks = blocks;
            _NoiseMap = noiseMap;
        }

        protected override void Process()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
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

        private static readonly OpenSimplexNoise NoiseFunction =
            new OpenSimplexNoise(WorldController.Current.WorldGenerationSettings.Seed);

        private void GenerateNormal(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            int noiseIndex = x + (Chunk.Size.x * z);

            float noiseHeight = _NoiseMap[noiseIndex];

            int perlinValue = Mathf.FloorToInt(noiseHeight * Chunk.Size.y);

            if (y > perlinValue)
            {
                return;
            }

            if ((y == perlinValue) || (y == (Chunk.Size.y - 1)))
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Grass");
            }
            else if ((y < perlinValue) && (y > (perlinValue - 5)))
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Dirt");
            }
            else if (y <= (perlinValue - 5))
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Stone");
            }
        }

        private void Generate3DSimplex(int index)
        {
            (int x, int y, int z) = Mathv.GetVector3IntIndex(index, Chunk.Size);

            double noiseValue =
                NoiseFunction.Evaluate((Position.x + x) / 20d, (Position.y + y) / 20d, (Position.z + z) / 20d);
            noiseValue /= y;

            if (noiseValue >= 0.2d)
            {
                _Blocks[index] = BlockController.Current.GetBlockId("Grass");
            }
        }
    }
}