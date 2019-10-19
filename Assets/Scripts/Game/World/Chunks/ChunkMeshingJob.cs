#region

using System.Collections.Generic;
using UnityEngine;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkMeshingJob : Job
    {
        private ChunkMesher _Mesher;

        /// <summary>
        ///     Prepares item for new execution.
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="blocks">Pre-initialized and built <see cref="T:ushort[]" /> to iterate through.</param>
        /// <param name="aggressiveFaceMerging"></param>
        /// <param name="isRemesh"></param>
        public void Set(Bounds bounds, IEnumerable<ushort> blocks, bool aggressiveFaceMerging, bool isRemesh = false)
        {
            if (_Mesher == null)
            {
                _Mesher = new ChunkMesher();
            }

            _Mesher.AbortToken = AbortToken;
            _Mesher.Bounds = bounds;
            _Mesher.EnumerableBlocks = blocks;
            _Mesher.Size = ChunkController.Size;
            _Mesher.AggressiveFaceMerging = aggressiveFaceMerging;
            _Mesher.ClearInternalData();
        }

        protected override void Process()
        {
            if (_Mesher.EnumerableBlocks == null)
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
                if (blocks[index].HasAnyFaces())
                {
                    blocks[index].ClearFaces();
                }
            }
        }
    }
}
