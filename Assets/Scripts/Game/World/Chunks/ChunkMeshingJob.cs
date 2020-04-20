#region

using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using Wyd.Controllers.System;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMeshingJob : AsyncJob
    {
        private readonly CancellationToken _CancellationToken;
        private readonly float3 _OriginPoint;
        private readonly OctreeNode<ushort> _Blocks;
        private readonly bool _AggressiveFaceMerging;

        private ChunkMesher _Mesher;

        public ChunkMeshingJob(CancellationToken cancellationToken, float3 originPoint, OctreeNode<ushort> blocks,
            bool aggressiveFaceMerging) : base(cancellationToken)
        {
            _CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken,
                cancellationToken).Token;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveFaceMerging;
        }

        protected override Task Process()
        {
            _Mesher = new ChunkMesher(_CancellationToken, _OriginPoint, _Blocks, _AggressiveFaceMerging);
            _Mesher.GenerateMesh();

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!_CancellationToken.IsCancellationRequested)
            {
                DiagnosticsController.Current.RollingMeshingSetBlockTimes.Enqueue(_Mesher.SetBlockTimeSpan);
                DiagnosticsController.Current.RollingMeshingTimes.Enqueue(_Mesher.MeshingTimeSpan);
            }

            return Task.CompletedTask;
        }

        public ChunkMeshData GetMeshData() => _Mesher.MeshData;
    }
}
