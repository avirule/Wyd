#region

using UnityEngine;
using Wyd.Controllers.World.Chunk;
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

            mesher.AbortToken = AbortToken;
            mesher.Size = ChunkController.Size;
            mesher.AggressiveFaceMerging = _AggressiveFaceMerging;
            mesher.ClearData();

            mesher.GenerateMesh(_GenerationData);

            _Mesher = mesher;
        }

        public void SetMesh(ref Mesh mesh)
        {
            _Mesher.SetMesh(ref mesh);
        }
    }
}
