#region

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Mathematics;
using Wyd.Controllers.State;
using Wyd.System.Collections;
using Random = System.Random;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public abstract class ChunkBuilder
    {
        protected readonly Dictionary<string, ushort> BlockIDCache;
        protected readonly Random SeededRandom;
        protected readonly Stopwatch Stopwatch;
        protected readonly CancellationToken CancellationToken;
        protected readonly int3 OriginPoint;

        protected INodeCollection<ushort> _Blocks;

        public ChunkBuilder(CancellationToken cancellationToken, int3 originPoint)
        {
            BlockIDCache = new Dictionary<string, ushort>();
            SeededRandom = new Random(originPoint.GetHashCode());
            Stopwatch = new Stopwatch();

            CancellationToken = cancellationToken;
            OriginPoint = originPoint;
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

            return BlockController.AirID;
        }

        public void GetGeneratedBlockData(out INodeCollection<ushort> blocks)
        {
            blocks = _Blocks;
        }
    }
}
