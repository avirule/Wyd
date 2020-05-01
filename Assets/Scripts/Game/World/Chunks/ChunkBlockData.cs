using Wyd.System.Collections;

namespace Wyd.Game.World.Chunks
{
    public class ChunkBlockData
    {
        public INodeCollection<ushort> Blocks { get; private set; }

        public ChunkBlockData()
        {
            Blocks = null;
        }

        public void SetBlockData(ref INodeCollection<ushort> blocks)
        {
            Blocks = blocks;
        }

        public void Deallocate()
        {
            Blocks = null;
        }
    }
}
