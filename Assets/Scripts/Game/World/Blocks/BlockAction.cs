#region

using Unity.Mathematics;

#endregion

namespace Wyd.Game.World.Blocks
{
    public struct BlockAction
    {
        public int3 GlobalPosition { get; private set; }
        public ushort Id { get; private set; }

        public BlockAction(int3 globalPosition, ushort id) => (GlobalPosition, Id) = (globalPosition, id);

        public void SetData(int3 globalPosition, ushort id) => (GlobalPosition, Id) = (globalPosition, id);
    }
}
