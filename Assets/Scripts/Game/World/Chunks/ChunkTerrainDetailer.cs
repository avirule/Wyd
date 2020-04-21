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

        public ChunkTerrainDetailer(CancellationToken cancellationToken, float3 originPoint,
            ref OctreeNode<ushort> blocks)
            : base(cancellationToken, originPoint) =>
            _Blocks = blocks;

        public void Detail()
        {
            Stopwatch.Restart();

            for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            {
                float3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);
                float3 globalPosition = OriginPoint + localPosition;

                if (_Blocks.UncheckedGetPoint(globalPosition) == BlockController.AirID)
                {
                    continue;
                }

                float noise2d = OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, 0.01f, globalPosition.xz);

                if (noise2d <= 0.5f)
                {
                    bool airAbove = true;

                    for (float3 ySteps = new float3(0f, 1f, 0f); ySteps.y <= 10; ySteps += Directions.Up)
                    {
                        if (TryGetPointBoundsAware(globalPosition + ySteps, out ushort blockId)
                            && (blockId != BlockController.AirID))
                        {
                            airAbove = false;
                            break;
                        }
                    }

                    if (airAbove)
                    {
                        _Blocks.UncheckedSetPoint(globalPosition, GetCachedBlockID("grass"));

                        for (float3 ySteps = new float3(0f, -1f, 0f);
                            ySteps.y >= -SeededRandom.Next(3, 5);
                            ySteps += Directions.Down)
                        {
                            SetPointBoundsAware(globalPosition + ySteps, GetCachedBlockID("dirt"));
                        }
                    }
                }

                if ((_Blocks.UncheckedGetPoint(globalPosition) == GetCachedBlockID("stone"))
                    && (OpenSimplexSlim.GetSimplex(WorldController.Current.Seed, 0.01f, globalPosition) > 0.5f))
                {
                    SetPointBoundsAware(globalPosition, GetCachedBlockID("coal_ore"));
                }
            }

            Stopwatch.Stop();

            TerrainDetailTimeSpan = Stopwatch.Elapsed;
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
                blockId = _Blocks.UncheckedGetPoint(globalPosition);
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
                _Blocks.UncheckedSetPoint(globalPosition, blockId);
            }
        }
    }
}
