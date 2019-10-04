#region

using Game.World.Blocks;
using Jobs;
using UnityEngine;

#endregion

namespace Game.World.Chunks
{
    public class ChunkMeshingJob : Job
    {
        private ChunkMesher _Mesher;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="meshData"></param>
        /// <param name="aggressiveFaceMerging"></param>
        /// <param name="isRemesh"></param>
        public void Set(Bounds bounds, Block[] blocks, ref MeshData meshData, bool aggressiveFaceMerging,
            bool isRemesh = false)
        {
            if (_Mesher == default)
            {
                _Mesher = new ChunkMesher();
            }

            if (isRemesh)
            {
                ClearAllFaces(blocks);
            }

            _Mesher.AbortToken = AbortToken;
            _Mesher.Bounds = bounds;
            _Mesher.Blocks = blocks;
            _Mesher.MeshData = meshData;
            _Mesher.AggressiveFaceMerging = aggressiveFaceMerging;
        }

        protected override void Process()
        {
            if (_Mesher.Blocks == default)
            {
                return;
            }

            _Mesher.GenerateMesh();
        }

        public void SetMesh(ref MeshData meshData)
        {
            //_Mesher.SetMesh(ref mesh);
        }

        private static void ClearAllFaces(Block[] blocks)
        {
            for (int index = 0; index < blocks.Length; index++)
            {
                if (blocks[index].HasAnyFaces())
                {
                    blocks[index].ClearFaces();
                }
            }
        }
    }
}
