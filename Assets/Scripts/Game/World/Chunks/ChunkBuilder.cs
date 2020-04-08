#region

using System.Collections.Generic;
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

        protected float3 _OriginPoint;
        protected OctreeNode<ushort> _Blocks;

        public ChunkBuilder(float3 originPoint, ref OctreeNode<ushort> blocks)
        {
            SeededRandom = new Random(WorldController.Current.Seed);
            _OriginPoint = originPoint;
            _Blocks = blocks;

            BlockIDCache = new Dictionary<string, ushort>();
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
