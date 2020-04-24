#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Mathematics;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkTerrainDetailer : ChunkBuilder
    {
        private List<INodeCollection<ushort>> _NeighborNodes;

        public TimeSpan TerrainDetailTimeSpan { get; private set; }

        public ChunkTerrainDetailer(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocks)
            : base(cancellationToken, originPoint)
        {
            _Blocks = blocks;

            _NeighborNodes = new List<INodeCollection<ushort>>(6);

            for (int i = 0; i < 6; i++)
            {
                _NeighborNodes.Add(null);
            }

            foreach ((int3 normal, ChunkController chunkController) in WorldController.Current.GetNeighboringChunksWithNormal(OriginPoint))
            {
                _NeighborNodes[GetNeighborIndexFromNormal(normal)] = chunkController.Blocks;
            }
        }

        public void Detail()
        {
            Stopwatch.Restart();

            for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            {
                int3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);
                float3 globalPosition = OriginPoint + localPosition;

                ushort blockId = _Blocks.GetPoint(localPosition);

                if ((blockId == BlockController.AirID)
                    || AttemptLaySurfaceBlocks(globalPosition, localPosition)) { }

                if (blockId == GetCachedBlockID("coal_ore"))
                {
                    int3 veinTipPosition = localPosition;
                    int lastSuccessfulIndexModulo = -1;


                    for (int veinLength = 0; veinLength < SeededRandom.Next(7, 12); veinLength++)
                    {
                        bool3 attempts = new bool3(false);
                        bool attemptSucceeded = false;

                        // ensure we can't end up going the same axial direction
                        if (lastSuccessfulIndexModulo > -1)
                        {
                            attempts[lastSuccessfulIndexModulo] = true;
                        }

                        while (!math.all(attempts))
                        {
                            int attemptedIndex;
                            int attemptedIndexModulo;

                            do
                            {
                                attemptedIndex = SeededRandom.Next(3, 6);
                            } while (attempts[attemptedIndexModulo = attemptedIndex % 3]);

                            attempts[attemptedIndexModulo] = true;

                            int3 faceNormal = GenerationConstants.FaceNormalByIteration[attemptedIndex];
                            int3 faceNormalVeinTipPosition = veinTipPosition + faceNormal;

                            if ((veinTipPosition[attemptedIndexModulo] < ChunkController.SIZE)
                                && (veinTipPosition[attemptedIndexModulo] >= 0)
                                && (_Blocks.GetPoint(faceNormalVeinTipPosition) == GetCachedBlockID("stone")))
                            {
                                _Blocks.SetPoint(faceNormalVeinTipPosition, GetCachedBlockID("coal_ore"));
                                veinTipPosition = faceNormalVeinTipPosition;
                                lastSuccessfulIndexModulo = attemptedIndexModulo;
                                attemptSucceeded = true;
                                break;
                            }
                        }

                        if (!attemptSucceeded)
                        {
                            break;
                        }
                    }
                }

                // if (_Blocks.UncheckedGetPoint(globalPosition) == GetCachedBlockID("stone"))
                // {
                //     float noise = OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, 0.001f, globalPosition);
                //     float powerNoise = math.pow(noise, 4f);
                //
                //     if ((powerNoise > 0.3f))
                //     {
                //         SetPointBoundsAware(globalPosition, GetCachedBlockID("coal_ore"));
                //     }
                // }
            }

            Stopwatch.Stop();

            TerrainDetailTimeSpan = Stopwatch.Elapsed;
        }

        private bool AttemptLaySurfaceBlocks(float3 globalPosition, float3 localPosition)
        {
            if ((globalPosition.y < (WorldController.WORLD_HEIGHT / 2f))
                || (OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, 0.01f, globalPosition.xz) > 0.5f))
            {
                return false;
            }

            bool airAbove = true;

            for (float3 ySteps = new float3(0f, 1f, 0f); ySteps.y <= 10; ySteps += Directions.Up)
            {
                if (GetPointBoundsAware(globalPosition + ySteps) == BlockController.AirID)
                {
                    continue;
                }

                airAbove = false;
                break;
            }

            if (!airAbove)
            {
                return false;
            }

            _Blocks.SetPoint(localPosition, GetCachedBlockID("grass"));

            for (float3 ySteps = new float3(0f, -1f, 0f);
                ySteps.y >= -SeededRandom.Next(3, 5);
                ySteps += Directions.Down)
            {
                SetPointBoundsAware(globalPosition + ySteps,
                    SeededRandom.Next(0, 8) == 0
                        ? GetCachedBlockID("dirt_coarse")
                        : GetCachedBlockID("dirt"));
            }

            return true;
        }

        #region Helper Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetNeighborIndexFromNormal(int3 normal)
        {
            int index = -1;

            // chunk index by normal value (from -1 to 1 on each axis):
            // positive: 1    4    0
            // negative: 3    5    2

            if (normal.x != 0)
            {
                index = normal.x > 0 ? 0 : 1;
            }
            else if (normal.y != 0)
            {
                index = normal.y > 0 ? 2 : 3;
            }
            else if (normal.z != 0)
            {
                index = normal.z > 0 ? 4 : 5;
            }

            return index;
        }

        private ushort GetPointBoundsAware(float3 globalPosition)
        {
            float3 localPosition = globalPosition - OriginPoint;

            if (math.any(localPosition < 0f) || math.any(localPosition >= ChunkController.SIZE))
            {
                return WorldController.Current.GetBlock(globalPosition);
            }
            else
            {
                return _Blocks.GetPoint(localPosition);
            }
        }

        private void SetPointBoundsAware(float3 globalPosition, ushort blockId)
        {
            float3 localPosition = globalPosition - OriginPoint;

            if (math.any(localPosition < 0f) || math.any(localPosition >= ChunkController.SIZE))
            {
                WorldController.Current.PlaceBlock(globalPosition, blockId);
            }
            else
            {
                _Blocks.SetPoint(localPosition, blockId);
            }
        }

        #endregion
    }
}
