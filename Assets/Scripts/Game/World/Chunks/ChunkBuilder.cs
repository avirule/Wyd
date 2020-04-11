#region

using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.System.Collections;
using Random = System.Random;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBuilder
    {
        protected readonly Dictionary<string, ushort> BlockIDCache;
        protected readonly Random SeededRandom;

        protected readonly CancellationToken CancellationToken;
        protected readonly float3 OriginPoint;
        protected readonly OctreeNode Blocks;

        public ChunkBuilder(CancellationToken cancellationToken, float3 originPoint, OctreeNode blocks)
        {
            BlockIDCache = new Dictionary<string, ushort>();
            SeededRandom = new Random(WorldController.Current.Seed);

            CancellationToken = cancellationToken;
            OriginPoint = originPoint;
            Blocks = blocks;
        }

        protected ushort GetCachedBlockID(string blockName)
        {
            if (BlockIDCache.TryGetValue(blockName, out ushort id))
            {
                return id;
            }
            else if (BlockController.Current.TryGetBlockId(blockName, out id))
            {
                BlockIDCache.Add(blockName, id);
                return id;
            }

            return BlockController.AIR_ID;
        }
    }
}
