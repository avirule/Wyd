#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Serilog;
using Wyd.Extensions;
using Wyd.World.Blocks;

#endregion

namespace Wyd.Controllers.State
{
    public class BlockController : SingletonController<BlockController>
    {
        public static ushort NullID;
        public static ushort AirID;

        public Dictionary<string, ushort> BlockNames;
        public List<IBlockDefinition> BlockDefinitions;

        private Dictionary<BlockDefinition.Property, HashSet<ushort>> _PropertiesBuckets;

        private void Awake()
        {
            AssignSingletonInstance(this);

            BlockNames = new Dictionary<string, ushort>();
            BlockDefinitions = new List<IBlockDefinition>();

            InitializeBlockPropertiesBuckets();

            RegisterBlockDefinition("null", null);
            RegisterBlockDefinition("air", null, BlockDefinition.Property.Transparent);

            TryGetBlockId("null", out NullID);
            TryGetBlockId("air", out AirID);
        }

        private void InitializeBlockPropertiesBuckets()
        {
            _PropertiesBuckets = new Dictionary<BlockDefinition.Property, HashSet<ushort>>();

            Log.Debug(
                $"Initializing property buckets for all '{nameof(BlockDefinition)}.{nameof(BlockDefinition.Property)}'s.");

            foreach (BlockDefinition.Property property in EnumExtensions.GetEnumsList<BlockDefinition.Property>())
            {
                _PropertiesBuckets.Add(property, new HashSet<ushort>());
            }
        }

        private void SortBlockDefinitionPropertiesToBuckets(BlockDefinition blockDefinition)
        {
            foreach (BlockDefinition.Property property in EnumExtensions.GetEnumsList<BlockDefinition.Property>())
            {
                if (blockDefinition.Properties.HasProperty(property))
                {
                    _PropertiesBuckets[property].Add(blockDefinition.Id);
                }
            }
        }

        /// <summary>
        ///     Registers a new <see cref="BlockDefinition" /> with the given parameters.
        /// </summary>
        /// <param name="blockName">
        ///     Friendly name for <see cref="BlockDefinition" />. NOTE: This value is automatically lowercased
        ///     upon registration.
        /// </param>
        /// <param name="uvsRule">Optional function to return custom textures for <see cref="BlockDefinition" />.</param>
        /// <param name="properties">
        ///     Optional <see cref="BlockDefinition.Property" />s to full qualify the
        ///     <see cref="BlockDefinition" />.
        /// </param>
        public void RegisterBlockDefinition(string blockName, Func<Direction, string> uvsRule, params BlockDefinition.Property[] properties)
        {
            blockName = blockName.ToLowerInvariant();

            ushort assignedBlockId;

            try
            {
                assignedBlockId = (ushort)BlockDefinitions.Count;
            }
            catch (OverflowException)
            {
                Log.Error($"{nameof(BlockController)} has registered too many blocks and is out of valid block ids.");
                return;
            }

            if (uvsRule == default)
            {
                uvsRule = direction => blockName;
            }

            BlockDefinition blockDefinition =
                new BlockDefinition(assignedBlockId, blockName, uvsRule, properties);

            BlockDefinitions.Add(blockDefinition);
            BlockNames.Add(blockName, assignedBlockId);
            SortBlockDefinitionPropertiesToBuckets(blockDefinition);

            Log.Information($"Successfully added block `{blockName}` with ID: {assignedBlockId}");
        }

        public bool GetUVs(ushort blockId, Direction direction, out ushort textureId)
        {
            if (!BlockIdExists(blockId))
            {
                throw new ArgumentOutOfRangeException(nameof(blockId), "Block ID does not exist.");
            }

            BlockDefinitions[blockId].GetUVs(direction, out string textureName);

            if (!TextureController.Current.TryGetTextureId(textureName, out textureId))
            {
                textureId = 0;
                return false;
            }
            else
            {
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BlockIdExists(ushort blockId) => blockId < BlockDefinitions.Count;

        public bool TryGetBlockId(string blockName, out ushort blockId)
        {
            blockId = 0;

            if (!BlockNames.TryGetValue(blockName, out blockId))
            {
                Log.Warning(
                    $"({nameof(BlockController)}) Failed to return block id for '{blockName}': block does not exist.");

                return false;
            }

            return true;
        }

        public bool TryGetBlockName(ushort blockId, out string blockName)
        {
            blockName = string.Empty;

            if (!BlockIdExists(blockId))
            {
                return false;
            }

            blockName = BlockDefinitions[blockId].BlockName;
            return true;
        }

        public IReadOnlyBlockDefinition GetBlockDefinition(ushort blockId)
        {
            if (!BlockIdExists(blockId))
            {
                throw new ArgumentException("Given block ID does not exist.", nameof(blockId));
            }

            return BlockDefinitions[blockId];
        }

        public bool TryGetBlockDefinition(ushort blockId, out IReadOnlyBlockDefinition blockDefinition)
        {
            if (BlockIdExists(blockId))
            {
                blockDefinition = BlockDefinitions[blockId];
                return true;
            }

            Log.Error($"Failed to return block rule for block with id `{blockId}`: block does not exist.");

            blockDefinition = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckBlockHasProperty(ushort blockId, BlockDefinition.Property property) =>
            (BlockDefinitions[blockId].Properties & property) == property;
    }
}
