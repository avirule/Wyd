using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using Wyd.Controllers.System;
using Wyd.System.Collections;
using Wyd.System.Jobs;

namespace Wyd.Game.World.Chunks
{
    public class ChunkDetailingJob : AsyncJob
    {
        private readonly float3 _OriginPoint;

        private OctreeNode<ushort> _Blocks;
        private ChunkTerrainDetailer _Detailer;

        public ChunkDetailingJob(CancellationToken cancellationToken, float3 originPoint,
            ref OctreeNode<ushort> blocks)
            : base(cancellationToken)
        {
            _OriginPoint = originPoint;
            _Blocks = blocks;
        }

        protected override Task Process()
        {
            _Detailer = new ChunkTerrainDetailer(CancellationToken, _OriginPoint, ref _Blocks);
            _Detailer.Detail();

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (_Detailer != null)
            {
                DiagnosticsController.Current.RollingTerrainDetailingTimes.Enqueue(_Detailer.TerrainDetailTimeSpan);
            }

            return Task.CompletedTask;
        }
    }
}
