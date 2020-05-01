#region

using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using Wyd.Controllers.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkTerrainDetailerJob : ChunkTerrainJob
    {
        private readonly INodeCollection<ushort> _Blocks;

        public ChunkTerrainDetailerJob(CancellationToken cancellationToken, int3 originPoint, INodeCollection<ushort> blocks)
            : base(cancellationToken, originPoint) => _Blocks = blocks;

        protected override Task Process()
        {
            ChunkTerrainDetailer detailer = new ChunkTerrainDetailer(CancellationToken, OriginPoint, _Blocks);
            detailer.Detail();

            // detailer has finished execution, so set
            _TerrainOperator = detailer;

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (_TerrainOperator != null)
            {
                ChunkTerrainDetailer detailer = (ChunkTerrainDetailer)_TerrainOperator;

                DiagnosticsController.Current.RollingTerrainDetailingTimes.Enqueue(detailer.TerrainDetailTimeSpan);
            }

            return Task.CompletedTask;
        }
    }
}
