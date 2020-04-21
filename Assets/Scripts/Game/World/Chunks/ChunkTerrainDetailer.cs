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

                if (noise2d > 0.5f)
                {
                    continue;
                }

                bool airAbove = true;

                for (float3 ySteps = new float3(0f, 1f, 0f); ySteps.y <= 10; ySteps += Directions.Up)
                {
                    float3 localStepped = localPosition + ySteps;
                    float3 globalStepped = globalPosition + ySteps;

                    if (
                        // check if local step is outside local blocks bounds
                        ((localStepped.y >= ChunkController.SIZE)
                         && WorldController.Current.TryGetBlock(globalStepped, out ushort queriedBlockId)
                         && (queriedBlockId != BlockController.AirID))
                        // check if local step is within local block bounds
                        || ((localStepped.y < ChunkController.SIZE)
                            && (_Blocks.UncheckedGetPoint(globalStepped) != BlockController.AirID)))
                    {
                        airAbove = false;
                        break;
                    }
                }

                if (airAbove)
                {
                    _Blocks.UncheckedSetPoint(globalPosition, GetCachedBlockID("grass"));

                    for (float3 ySteps = new float3(0f, -1f, 0f); ySteps.y >= -SeededRandom.Next(3, 5); ySteps += Directions.Down)
                    {
                        float3 localStepped = localPosition + ySteps;
                        float3 globalStepped = globalPosition + ySteps;

                        if (localStepped.y < 0f)
                        {
                            WorldController.Current.TryPlaceBlock(globalStepped, GetCachedBlockID("dirt"));
                        }
                        else
                        {
                            _Blocks.UncheckedSetPoint(globalStepped, GetCachedBlockID("dirt"));
                        }
                    }
                }
            }

            Stopwatch.Stop();

            TerrainDetailTimeSpan = Stopwatch.Elapsed;
        }
    }
}
