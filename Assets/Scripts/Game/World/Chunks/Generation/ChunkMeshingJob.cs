#region

using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public class ChunkMeshingJob : AsyncJob
    {
        private static readonly ObjectCache<ChunkMesher> _chunkMesherCache = new ObjectCache<ChunkMesher>();

        private readonly CancellationToken _CancellationToken;
        private readonly float3 _OriginPoint;
        private readonly INodeCollection<ushort> _Blocks;
        private readonly bool _AggressiveFaceMerging;

        private ChunkMesher _Mesher;

        public ChunkMeshingJob(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocks,
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
            if (_chunkMesherCache.TryRetrieve(out ChunkMesher mesher))
            {
                mesher.PrepareMeshing(_CancellationToken, _OriginPoint, _Blocks, _AggressiveFaceMerging);
            }
            else
            {
                mesher = new ChunkMesher(_CancellationToken, _OriginPoint, _Blocks, _AggressiveFaceMerging);
            }

            mesher.GenerateMesh();
            _Mesher = mesher;

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!_CancellationToken.IsCancellationRequested && (_Mesher != null))
            {
                DiagnosticsController.Current.RollingMeshingSetBlockTimes.Enqueue(_Mesher.SetBlockTimeSpan);
                DiagnosticsController.Current.RollingMeshingTimes.Enqueue(_Mesher.MeshingTimeSpan);
            }

            return Task.CompletedTask;
        }

        public void ApplyMeshData(ref Mesh mesh) => _Mesher?.ApplyMeshData(ref mesh);

        public void CacheMesher()
        {
            _Mesher?.Reset();
            _chunkMesherCache.CacheItem(ref _Mesher);
            _Mesher = null;
        }
    }
}
