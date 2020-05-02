#region

using System;
using System.Threading;
using Unity.Mathematics;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks.Generation
{
    public abstract class ChunkTerrainJob : AsyncJob
    {
        protected int3 _OriginPoint;

        protected ChunkBuilder _TerrainGenerator;

        public void SetData(CancellationToken cancellationToken, int3 originPoint)
        {
            CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken, cancellationToken).Token;
            _OriginPoint = originPoint;
        }

        public void GetGeneratedBlockData(out INodeCollection<ushort> blocks)
        {
            if (_TerrainGenerator == null)
            {
                throw new NullReferenceException(
                    $"'{nameof(ChunkBuilder)}' is null. This likely indicates the job has not completed execution.");
            }

            _TerrainGenerator.GetGeneratedBlockData(out blocks);
        }
    }
}
