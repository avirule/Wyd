#region

using Wyd.System.Collections;

#endregion

namespace Wyd.Game.World.Chunks
{
    public class ChunkBlockData
    {
        public INodeCollection<ushort> Blocks { get; set; }

        public ChunkBlockData() => Blocks = null;

        public void Deallocate()
        {
            Blocks = null;
        }
    }
}
