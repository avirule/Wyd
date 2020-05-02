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

        private float3 _OriginPoint;
        private INodeCollection<ushort> _Blocks;
        private bool _AggressiveFaceMerging;

        private ChunkMesher _Mesher;

        public void SetData(CancellationToken cancellationToken, float3 originPoint, INodeCollection<ushort> blocks,
            bool aggressiveFaceMerging)
        {
            CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken, cancellationToken).Token;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveFaceMerging;
        }

        protected override Task Process()
        {
            if (_chunkMesherCache.TryRetrieve(out ChunkMesher mesher))
            {
                mesher.PrepareMeshing(CancellationToken, _OriginPoint, _Blocks, _AggressiveFaceMerging);
            }
            else
            {
                mesher = new ChunkMesher(CancellationToken, _OriginPoint, _Blocks, _AggressiveFaceMerging);
            }

            mesher.GenerateMesh();
            _Mesher = mesher;

            return Task.CompletedTask;
        }

        protected override Task ProcessFinished()
        {
            if (!CancellationToken.IsCancellationRequested && (_Mesher != null))
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
            _chunkMesherCache.CacheItem(_Mesher);
            _Mesher = null;
            _Blocks = null;
            
        }
    }
}
