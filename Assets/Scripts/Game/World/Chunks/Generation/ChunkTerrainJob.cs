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
        protected readonly int3 OriginPoint;

        protected ChunkBuilder _TerrainOperator;

        protected ChunkTerrainJob(CancellationToken cancellationToken, int3 originPoint)
            : base(cancellationToken) => OriginPoint = originPoint;

        public INodeCollection<ushort> GetGeneratedBlockData()
        {
            if (_TerrainOperator == null)
            {
                throw new NullReferenceException(
                    $"'{nameof(ChunkBuilder)}' is null. This likely indicates the job has not completed execution.");
            }

            return _TerrainOperator.GetGeneratedBlockData();
        }

        public void DisposeOperator()
        {
            _TerrainOperator.Dispose();
            _TerrainOperator = null;
        }
    }
}
