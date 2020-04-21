#region

using System;
using System.Threading;
using Unity.Mathematics;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Game.World.Chunks
{
    public abstract class ChunkBuilderJob : AsyncJob
    {
        protected readonly float3 OriginPoint;

        protected ChunkBuilder _TerrainOperator;

        protected ChunkBuilderJob(CancellationToken cancellationToken, float3 originPoint)
            : base(CancellationTokenSource.CreateLinkedTokenSource(AsyncJobScheduler.AbortToken,
                cancellationToken).Token) =>
            OriginPoint = originPoint;

        public void GetGeneratedBlockData(out OctreeNode<ushort> blocks)
        {
            if (_TerrainOperator == null)
            {
                throw new NullReferenceException(
                    $"'{nameof(ChunkBuilder)}' is null. This likely indicates the job has not completed execution.");
            }

            _TerrainOperator.GetGeneratedBlockData(out blocks);
        }
    }
}
