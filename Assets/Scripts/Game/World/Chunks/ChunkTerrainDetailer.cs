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

        public ChunkTerrainDetailer(CancellationToken cancellationToken, float3 originPoint, OctreeNode<ushort> blocks)
            : base(cancellationToken, originPoint) =>
            _Blocks = blocks;

        public void Detail()
        {
            Stopwatch.Restart();

            for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            {
                float3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);
                float3 globalPosition = OriginPoint + localPosition;

                if (_Blocks.GetPoint(localPosition) == BlockController.AirID)
                {
                    continue;
                }

                AttemptLaySurfaceBlocks(globalPosition, localPosition);

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

        private void AttemptLaySurfaceBlocks(float3 globalPosition, float3 localPosition)
        {
            if ((globalPosition.y < (WorldController.WORLD_HEIGHT / 2f))
                || (OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, 0.01f, globalPosition.xz) > 0.5f))
            {
                return;
            }

            bool airAbove = true;

            for (float3 ySteps = new float3(0f, 1f, 0f); ySteps.y <= 10; ySteps += Directions.Up)
            {
                if (TryGetPointBoundsAware(globalPosition + ySteps, out ushort blockId)
                    && (blockId == BlockController.AirID))
                {
                    continue;
                }

                airAbove = false;
                break;
            }

            if (!airAbove)
            {
                return;
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
        }

        private bool TryGetPointBoundsAware(float3 globalPosition, out ushort blockId)
        {
            float3 localPosition = globalPosition - OriginPoint;

            if (math.any(localPosition < 0f) || math.any(localPosition >= ChunkController.SIZE))
            {
                return WorldController.Current.TryGetBlock(globalPosition, out blockId);
            }
            else
            {
                blockId = _Blocks.GetPoint(localPosition);
                return true;
            }
        }

        private void SetPointBoundsAware(float3 globalPosition, ushort blockId)
        {
            float3 localPosition = globalPosition - OriginPoint;

            if (math.any(localPosition < 0f) || math.any(localPosition >= ChunkController.SIZE))
            {
                WorldController.Current.TryPlaceBlock(globalPosition, blockId);
            }
            else
            {
                _Blocks.SetPoint(localPosition, blockId);
            }
        }
    }
}
