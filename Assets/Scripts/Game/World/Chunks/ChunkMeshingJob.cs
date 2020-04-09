#region

using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
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
            _CancellationToken = cancellationToken;
            _OriginPoint = originPoint;
            _Blocks = blocks;
            _AggressiveFaceMerging = aggressiveFaceMerging;
        }

        protected override Task Process()
        {
            _Mesher = new ChunkMesher(_CancellationToken, _OriginPoint, _Blocks, AsyncJobScheduler.AbortToken,
                _AggressiveFaceMerging);
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

        public bool SetMesh(ref Mesh mesh)
        {
            if (_Mesher != null)
            {
                _Mesher.SetMesh(ref mesh);
                _Mesher.ClearExistingData();
                _Mesher = null;
                return true;
            }
            else
            {
                Log.Error(
                    $"Attempted to use `{nameof(SetMesh)}` when no ChunkMesher has been processed. This could be from a previous `{nameof(SetMesh)}` call, or improper initialization.");
                return false;
            }
        }
    }
}
