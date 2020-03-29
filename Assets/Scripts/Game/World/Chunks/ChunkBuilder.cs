#region

using System;
using Wyd.Controllers.World;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBuilder
    {
        protected static readonly ObjectCache<ChunkBuilderNoiseValues> NoiseValuesCache =
            new ObjectCache<ChunkBuilderNoiseValues>();

        protected Random _Rand;
        protected GenerationData _GenerationData;

        /// <summary>
        ///     Prepares job for new execution.
        /// </summary>
        public void SetGenerationData(GenerationData generationData)
        {
            _Rand = new Random(WorldController.Current.Seed);
            _GenerationData = generationData;
        }
    }
}
