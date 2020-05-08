#region

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Unity.Mathematics;
using Wyd.Controllers.State;
using Wyd.System.Collections;
using Wyd.System.Jobs;
using Random = System.Random;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public abstract class ChunkTerrainJob : AsyncJob
    {
        private static readonly Dictionary<string, ushort> _blockIDCache = new Dictionary<string, ushort>();

        protected readonly Stopwatch Stopwatch;

        protected int3 _OriginPoint;
        protected Random _SeededRandom;
        protected INodeCollection<ushort> _Blocks;

        protected ChunkTerrainJob() => Stopwatch = new Stopwatch();

        protected void SetData(CancellationToken cancellationToken, int3 originPoint)
        {
            CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken, cancellationToken).Token;
            _OriginPoint = originPoint;

            _SeededRandom = new Random(_OriginPoint.GetHashCode());
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
    }
}
