#region

using System.Threading;
using Unity.Mathematics;
using Wyd.Controllers.World.Chunk;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkTerrainDetailer : ChunkBuilder
    {
        public ChunkTerrainDetailer(CancellationToken cancellationToken, float3 originPoint,
            ref OctreeNode<ushort> blocks)
            : base(cancellationToken, originPoint)
        {
            _Blocks = blocks;
        }

        public void Detail()
        {
            for (int index = 0; index < ChunkController.SIZE_CUBED; index++)
            {
                float3 localPosition = WydMath.IndexTo3D(index, ChunkController.SIZE);


            }
        }
    }
}
