#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Unity.Mathematics;
using Wyd.Collections;
using Wyd.Controllers.State;
using Wyd.Jobs;
using Random = System.Random;

#endregion

namespace Wyd.World.Chunks.Generation
{
    public abstract class ChunkTerrainJob : AsyncParallelJob
    {
        private static readonly ConcurrentDictionary<string, ushort> _blockIDCache = new ConcurrentDictionary<string, ushort>();

        protected readonly Stopwatch Stopwatch;

        protected int3 _OriginPoint;
        protected Random _SeededRandom;
        protected INodeCollection<ushort> _Blocks;

        protected ChunkTerrainJob() : base(GenerationConstants.CHUNK_SIZE_CUBED, 128) => Stopwatch = new Stopwatch();

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
                _blockIDCache.TryAdd(blockName, id);
                return id;
            }

            return BlockController.AirID;
        }

        public INodeCollection<ushort> GetGeneratedBlockData() => _Blocks;
    }
}
