#region

using Game.World.Blocks;
using Game.World.Chunks;
using UnityEngine;

#endregion

namespace Threading
{
    public class ChunkMeshingThreadedItem : ThreadedItem
    {
        private ChunkMesher _Mesher;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="position"><see cref="UnityEngine.Vector3" /> position of chunk being meshed.</param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="aggressiveFaceMerging"></param>
        /// <param name="isRemesh"></param>
        public void Set(Vector3 position, Block[] blocks, bool aggressiveFaceMerging, bool isRemesh = false)
        {
            if (_Mesher == default)
            {
                _Mesher = new ChunkMesher();
            }

            if (isRemesh)
            {
                ClearAllFaces(blocks);
            }

            _Mesher.ClearInternalData();
            _Mesher.AbortToken = AbortToken;
            _Mesher.Position.Set(position.x, position.y, position.z);
            _Mesher.Blocks = blocks;
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

        public void SetMesh(ref Mesh mesh)
        {
            _Mesher.SetMesh(ref mesh);
        }

        private static void ClearAllFaces(Block[] blocks)
        {
            for (int index = 0; index < blocks.Length; index++)
            {
                blocks[index].ClearFaces();
            }
        }
    }
}