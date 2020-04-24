#region

using System;
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
        public TimeSpan TerrainDetailTimeSpan { get; private set; }

        public ChunkTerrainDetailer(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocks)
            : base(cancellationToken, originPoint) =>
            _Blocks = blocks;

        public void Detail()
        {
            Stopwatch.Restart();

            for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            {
                float3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);
                float3 globalPosition = OriginPoint + localPosition;

                ushort blockId = _Blocks.GetPoint(localPosition);

                if ((blockId == BlockController.AirID)
                    || AttemptLaySurfaceBlocks(globalPosition, localPosition)) { }

                // if (blockId == GetCachedBlockID("coal_ore"))
                // {
                //     for (int veinLength = 0; veinLength < 7; veinLength++)
                //     {
                //         for (int sign = 0; sign < 2; sign++)
                //         {
                //             if (sign == 0)
                //             {
                //                 continue;
                //             }
                //
                //             for (int i = 0; i < 3; i++)
                //             {
                //                 //float3 normal =
                //             }
                //         }
                //     }
                // }

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
    }
}
