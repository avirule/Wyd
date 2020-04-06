#region

using System;
using System.Collections.Generic;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBuilder
    {
        protected static readonly ObjectCache<NoiseMap> NoiseValuesCache =
            new ObjectCache<NoiseMap>();

        protected Random _Rand;
        protected readonly Dictionary<string, ushort> BlockIDCache;
        protected GenerationData _GenerationData;

        public ChunkBuilder() => BlockIDCache = new Dictionary<string, ushort>();

        /// <summary>
        ///     Prepares job for new execution.
        /// </summary>
        public void SetGenerationData(GenerationData generationData)
        {
            _Rand = new Random(WorldController.Current.Seed);
            _GenerationData = generationData;
        }

        protected ushort GetCachedBlockID(string blockName)
        {
            if (BlockIDCache.TryGetValue(blockName, out ushort id))
            {
                return id;
            }
            else if (BlockController.Current.TryGetBlockId(blockName, out id))
            {
                BlockIDCache.Add(blockName, id);
                return id;
            }

            return BlockController.AIR_ID;
        }
    }
}
