#region

using System.Collections.Generic;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Game.World.Blocks;
using Wyd.System;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkLayer
    {
        private readonly Block[] _Blocks;
        private readonly HashSet<ushort> _ContainedBlockIDs;

        private ushort _SolidBlockCount;

        public bool HasNonAirBlocks => _ContainedBlockIDs.Count > 0;
        public bool IsSolid => _SolidBlockCount == _Blocks.Length;

        public Block this[int index] => _Blocks[index];

        public ChunkLayer()
        {
            _Blocks = new Block[ChunkController.Size.x * ChunkController.Size.z];
            _ContainedBlockIDs = new HashSet<ushort>();
        }

        public void ModifyBlock(Vector3 position, ushort newId)
        {
            position.y = 0f;
            int index = position.To1D(ChunkController.Size, true);

            if (index >= _Blocks.Length)
            {
                return;
            }

            ushort airId = BlockController.Air.Id;

            if (newId != airId)
            {
                _ContainedBlockIDs.Add(newId);
                _SolidBlockCount += 1;
            }
            else if (_Blocks[index].Id == airId)
            {
                _ContainedBlockIDs.Remove(_Blocks[index].Id);
                _Blocks[index].Initialise(newId);
                _SolidBlockCount -= 1;
            }
        }

        public void Clear()
        {
            for (int index = 0; index < _Blocks.Length; index++)
            {
                _Blocks[index] = BlockController.Air;
            }
        }
    }
}
