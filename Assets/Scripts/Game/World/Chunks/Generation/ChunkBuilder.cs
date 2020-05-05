#region

using System;
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
    public abstract class ChunkBuilder : IDisposable
    {
        private static readonly Dictionary<string, ushort> _blockIDCache = new Dictionary<string, ushort>();

        protected readonly CancellationToken CancellationToken;
        protected readonly Random SeededRandom;
        protected readonly Stopwatch Stopwatch;
        protected readonly int3 OriginPoint;

        protected INodeCollection<ushort> _Blocks;

        protected ChunkBuilder(CancellationToken cancellationToken, int3 originPoint)
        {
            SeededRandom = new Random(originPoint.GetHashCode());
            Stopwatch = new Stopwatch();

            CancellationToken = cancellationToken;
            OriginPoint = originPoint;
        }

        protected static ushort GetCachedBlockID(string blockName)
        {
            if (_blockIDCache.TryGetValue(blockName, out ushort id))
            {
                return id;
            }
            else if (BlockController.Current.TryGetBlockId(blockName, out id))
            {
                _blockIDCache.Add(blockName, id);
                return id;
            }

            return BlockController.AirID;
        }

        public INodeCollection<ushort> GetGeneratedBlockData() => _Blocks;

        public virtual void Dispose()
        {
            _Blocks = null;
        }
    }
}
