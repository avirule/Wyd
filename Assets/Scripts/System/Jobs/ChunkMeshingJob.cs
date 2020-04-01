#region

using Serilog;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.Game.World.Chunks;
using Wyd.System.Collections;

#endregion

namespace Wyd.System.Jobs
{
    public class ChunkMeshingJob : Job
    {
        private static readonly ObjectCache<ChunkMesher> _ChunkMesherCache = new ObjectCache<ChunkMesher>();

        private ChunkMesher _Mesher;
        private readonly GenerationData _GenerationData;
        private readonly bool _AggressiveFaceMerging;

        public ChunkMeshingJob(GenerationData generationData, bool aggressiveFaceMerging)
        {
            _Mesher = null;
            _GenerationData = generationData;
            _AggressiveFaceMerging = aggressiveFaceMerging;
        }

        protected override void Process()
        {
            ChunkMesher mesher = _ChunkMesherCache.Retrieve() ?? new ChunkMesher();
            mesher.SetRuntimeFields(_GenerationData, AbortToken, _AggressiveFaceMerging);
            mesher.ClearExistingData();
            mesher.GenerateMesh();

            _Mesher = mesher;
        }

        protected override void ProcessFinished()
        {
            DiagnosticsController.Current.RollingMeshingSetBlockTimes.Enqueue(_Mesher.SetBlockTimeSpan);
            DiagnosticsController.Current.RollingMeshingTimes.Enqueue(_Mesher.MeshingTimeSpan);
        }

        public bool SetMesh(ref Mesh mesh)
        {
            if (_Mesher != null)
            {
                _Mesher.SetMesh(ref mesh);
                _ChunkMesherCache.CacheItem(ref _Mesher);
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
